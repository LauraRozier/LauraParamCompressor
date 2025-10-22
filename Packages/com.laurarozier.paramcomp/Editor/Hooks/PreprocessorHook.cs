#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                Debug.LogWarning("Skipping `PreprocessorHook` because preprocessors already ran on this object");
                return true;
            }

            StateHolder.SetProcessed(obj);

            VRCAvatarDescriptor avatar = obj.GetComponent<VRCAvatarDescriptor>();
            var paramDef = avatar.expressionParameters;
            var runtimeCtrl = avatar.baseAnimationLayers?.FirstOrDefault(
                    bal => bal.type == VRCAvatarDescriptor.AnimLayerType.FX
                ).animatorController;
            var animCtrl = runtimeCtrl != null
                ? AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(runtimeCtrl))
                : null;

            StringBuilder sb = new();
            sb.AppendLine("Laura Testing:");
            sb.AppendLine($"- Avatar FX Controller = {animCtrl.name}");
            sb.AppendLine($"- Avatar Parameters = {paramDef.name}");
            sb.AppendLine("- Synced Params:");

            foreach (var param in paramDef.parameters)
            {
                if (!param.networkSynced) continue;

                sb.AppendLine($"  > {param.name} [{param.valueType}]");
            }

            Debug.Log(sb.ToString());
            return true;
        }
    }
}
#endif
