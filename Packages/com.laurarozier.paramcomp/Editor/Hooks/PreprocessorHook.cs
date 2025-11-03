#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ParamComp.Runtime.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase.Editor.BuildPipeline;

namespace ParamComp.Editor.Hooks
{
    internal class PreprocessorHook : IVRCSDKPreprocessAvatarCallback
    {
        private const string CVRCFuryTempFolder = "com.vrcfury.temp";
        public int callbackOrder => int.MaxValue - 105; // VRCFury's compressor is `int.MaxValue - 100`

        public bool OnPreprocessAvatar(GameObject obj) {
            if (!StateHolder.ShouldProcess(obj)) {
                Debug.LogWarning($"{ParamComp.CLogPrefix} Skipping `PreprocessorHook` because preprocessors already ran on this object");
                return true;
            }

            if (!obj.TryGetComponent(out ParamCompSettings settings)) {
                Debug.Log($"{ParamComp.CLogPrefix} Skipping `PreprocessorHook` because `ParamComp Settings` component is not found on the avatar");
                return true;
            }

            Debug.Log($"{ParamComp.CLogPrefix} `PreprocessorHook` running for {obj.name}");
            StateHolder.SetProcessed(obj);

            var (paramDef, animCtrl) = GetRequiredAssets(obj.GetComponent<VRCAvatarDescriptor>());

            UtilParameters exprParams = new();
            exprParams.SetValues(paramDef);
            var boolsPerState = settings.BoolsPerState;
            var numbersPerState = settings.NumbersPerState;

            for (int i = 0; i < exprParams.Parameters.Count; i++) {
                exprParams.Parameters[i] = ProcessExclusions(exprParams.Parameters[i], settings);
            }

            // Remove our settings component so it doesn't get uploaded
            UnityEngine.Object.DestroyImmediate(settings);
            ParamComp.PerformCompression(exprParams, animCtrl, paramDef, numbersPerState, boolsPerState, true);
            return true;
        }

        private (VRCExpressionParameters, AnimatorController) GetRequiredAssets(VRCAvatarDescriptor avatar) {
            var paramDef = avatar.expressionParameters;
            var paramDefPath = AssetDatabase.GetAssetPath(paramDef);

            // Only make a laurafied version if it's NOT a vrcFury asset
            if (!paramDefPath.Contains(CVRCFuryTempFolder)) {
                paramDefPath = CloneAsset(paramDefPath);
                paramDef = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(paramDefPath);
                avatar.expressionParameters = paramDef;
            }

            var runtimeCtrl = avatar.baseAnimationLayers?.FirstOrDefault(
                bal => bal.type == VRCAvatarDescriptor.AnimLayerType.FX
            ).animatorController;
            var fxControllerPath = AssetDatabase.GetAssetPath(runtimeCtrl);

            // Only make a laurafied version if it's NOT a vrcFury asset
            if (!fxControllerPath.Contains(CVRCFuryTempFolder)) {
                fxControllerPath = CloneAsset(fxControllerPath);
                runtimeCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(fxControllerPath);

                for (int i = 0; i < avatar.baseAnimationLayers.Length; i++) {
                    if (avatar.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX) {
                        var layer = avatar.baseAnimationLayers[i];
                        layer.animatorController = runtimeCtrl;
                        avatar.baseAnimationLayers[i] = layer;
                        break;
                    }
                }
            }

            var animCtrl = runtimeCtrl != null
                ? AssetDatabase.LoadAssetAtPath<AnimatorController>(fxControllerPath)
                : null;
            return (paramDef, animCtrl);
        }

        private static string CloneAsset(string oldPath) {
            var newPath = Path.Combine(
                Path.GetDirectoryName(oldPath),
                $"{Path.GetFileNameWithoutExtension(oldPath)}_laurafied.{Path.GetExtension(oldPath)}"
            );
            return AssetDatabase.CopyAsset(oldPath, newPath) ? newPath : oldPath;
        }

        private UtilParameterInfo ProcessExclusions(UtilParameterInfo param, ParamCompSettings settings) {
            if ((settings.ExcludeBools && param.SourceParam.valueType == VRCExpressionParameters.ValueType.Bool) ||
                (settings.ExcludeInts && param.SourceParam.valueType == VRCExpressionParameters.ValueType.Int) ||
                (settings.ExcludeFloats && param.SourceParam.valueType == VRCExpressionParameters.ValueType.Float)
            ) return param.Disable();

            if (settings.ExcludeVRCFT && (
                param.SourceParam.name.StartsWith("FT/v1/", StringComparison.InvariantCultureIgnoreCase) ||
                param.SourceParam.name.StartsWith("FT/v2/", StringComparison.InvariantCultureIgnoreCase) ||
                param.SourceParam.name.StartsWith("FT/v3/", StringComparison.InvariantCultureIgnoreCase) // Why not future-proof a little
            )) return param.Disable();

            if (settings.ExcludedPropertyNamePrefixes.Any(prefix => param.SourceParam.name.StartsWith(
                    prefix, StringComparison.InvariantCultureIgnoreCase
                )) ||
                settings.ExcludedPropertyNameSuffixes.Any(suffix => param.SourceParam.name.EndsWith(
                    suffix, StringComparison.InvariantCultureIgnoreCase
                )) ||
                settings.ExcludedPropertyNames.Contains(param.SourceParam.name)
            ) return param.Disable();

            return param;
        }
    }
}
#endif
