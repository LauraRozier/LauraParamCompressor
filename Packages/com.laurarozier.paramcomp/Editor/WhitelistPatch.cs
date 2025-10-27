#if UNITY_EDITOR
using ParamComp.Runtime.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ParamComp.Editor.Components
{
    // Based on https://github.com/VRCFury/VRCFury/blob/19dcea72e494c7dc6d265259acddda9057ddfd4c/com.vrcfury.vrcfury/Editor/VF/VrcfEditorOnly/WhitelistPatch.cs
    internal static class WhitelistPatch
    {
        [InitializeOnLoadMethod]
        private static void Init() {
            Exception preprocessPatchEx = null;

            try {
                Debug.Log("Checking new whitelist ...");
                var validation = GetTypeFromAnyAssembly("VRC.SDKBase.Validation.AvatarValidation");
                var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var whitelist = whitelistField.GetValue(null);
                whitelistField.SetValue(null, UpdateComponentList((string[])whitelist));
            } catch (Exception e) {
                preprocessPatchEx = e;
            }

            try {
                Debug.Log("Checking old whitelist ...");
                var validation = GetTypeFromAnyAssembly("VRC.SDK3.Validation.AvatarValidation");
                var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var whitelist = whitelistField.GetValue(null);
                whitelistField.SetValue(null, UpdateComponentList((string[])whitelist));
            } catch (Exception) {
                if (preprocessPatchEx != null) {
                    Debug.LogError(new Exception("Laura's Parameter Compressor preprocess patch failed", preprocessPatchEx));
                }
            }

            // This is purely here because some other addons initialize the vrcsdk whitelist cache for some reason
            try {
                Debug.Log("Clearing whitelist cache ...");
                var validation = GetTypeFromAnyAssembly("VRC.SDKBase.Validation.ValidationUtils");
                var cachedWhitelists = validation.GetField("_whitelistCache",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var whitelists = cachedWhitelists.GetValue(null);
                var clearMethod = whitelists.GetType().GetMethod("Clear");
                clearMethod.Invoke(whitelists, Array.Empty<object>());
            } catch (Exception e) {
                Debug.LogError(new Exception("Laura's Parameter Compressor failed to clear whitelist cache", e));
            }
        }

        private static string[] UpdateComponentList(string[] list) =>
            new List<string>(list) { typeof(ParamCompSettings).FullName }.ToArray();

        public static Type GetTypeFromAnyAssembly(string type) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(type))
                .FirstOrDefault(t => t != null);
    }
}
#endif
