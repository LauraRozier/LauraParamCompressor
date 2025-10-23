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
        public int callbackOrder =>
            int.MaxValue - 110;

        public bool OnPreprocessAvatar(GameObject obj)
        {
            if (!StateHolder.ShouldProcess(obj))
            {
                Debug.LogWarning("ParamComp - Skipping `PreprocessorHook` because preprocessors already ran on this object");
                return true;
            }

            if (!obj.TryGetComponent(out ParamCompSettings pcSettings))
            {
                Debug.LogWarning("ParamComp - Skipping `PreprocessorHook` because `ParamComp Settings` component is not found on the avatar");
                return true;
            }

            Debug.LogWarning($"ParamComp - `PreprocessorHook` running for {obj.name}");
            StateHolder.SetProcessed(obj);

            VRCAvatarDescriptor avatar = obj.GetComponent<VRCAvatarDescriptor>();
            var paramDef = avatar.expressionParameters;
            var paramDefPath = AssetDatabase.GetAssetPath(paramDef);

            // Only make a laurafied version if it's NOT a vrcFury asset
            if (!paramDefPath.Contains("com.vrcfury.temp"))
            {
                paramDefPath = CloneAsset(paramDefPath);
                paramDef = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(paramDefPath);
                avatar.expressionParameters = paramDef;
            }

            var runtimeCtrl = avatar.baseAnimationLayers?.FirstOrDefault(
                    bal => bal.type == VRCAvatarDescriptor.AnimLayerType.FX
                ).animatorController;
            var fxControllerPath = AssetDatabase.GetAssetPath(runtimeCtrl);

            // Only make a laurafied version if it's NOT a vrcFury asset
            if (!fxControllerPath.Contains("com.vrcfury.temp"))
            {
                fxControllerPath = CloneAsset(fxControllerPath);
                runtimeCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(fxControllerPath);

                for (int i = 0; i < avatar.baseAnimationLayers.Length; i++)
                {
                    if (avatar.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                    {
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
            UtilParameters exprParams = new();
            exprParams.SetValues(paramDef);

            for (int i = 0; i < exprParams.Parameters.Count; i++)
            {
                var param = exprParams.Parameters[i];

                if (pcSettings.ExcludedPropertyNames.Contains(param.SourceParam.name))
                    param.EnableProcessing = false;

                if (pcSettings.ExcludeBools && param.SourceParam.valueType == VRCExpressionParameters.ValueType.Bool)
                    param.EnableProcessing = false;

                if (pcSettings.ExcludeInts && param.SourceParam.valueType == VRCExpressionParameters.ValueType.Int)
                    param.EnableProcessing = false;

                if (pcSettings.ExcludeFloats && param.SourceParam.valueType == VRCExpressionParameters.ValueType.Float)
                    param.EnableProcessing = false;

                exprParams.Parameters[i] = param;
            }

            // Remove our settings component so it doesn't get uploaded
            UnityEngine.Object.DestroyImmediate(pcSettings);
            ParamComp.PerformCompression(exprParams, animCtrl, paramDef, true);
            return true;
        }

        private static string CloneAsset(string oldPath)
        {
            var newPath = Path.Combine(
                Path.GetDirectoryName(oldPath),
                $"{Path.GetFileNameWithoutExtension(oldPath)}_laurafied.{Path.GetExtension(oldPath)}"
            );
            AssetDatabase.CopyAsset(oldPath, newPath);
            return newPath;
        }
    }
}
#endif
