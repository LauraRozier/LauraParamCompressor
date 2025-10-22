#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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

            Debug.Log($"Laura Testing:\n- Avatar Parameters = {paramDef.name}\n- Avatar FX Controller = {animCtrl.name}");

            return true;
        }
    }
}
#endif
