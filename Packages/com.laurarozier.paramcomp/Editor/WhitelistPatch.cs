#if UNITY_EDITOR
using ParamComp.Runtime.Components;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ParamComp.Editor.Components
{
    // Based on https://github.com/VRCFury/VRCFury/blob/19dcea72e494c7dc6d265259acddda9057ddfd4c/com.vrcfury.vrcfury/Editor/VF/VrcfEditorOnly/WhitelistPatch.cs
    internal static class WhitelistPatch
    {
        private readonly static string CSettingsName = typeof(ParamCompSettings).FullName;

        [InitializeOnLoadMethod]
        static void Init() {
            try {
                Debug.Log($"{ParamComp.CLogPrefix} Attempting to patch the whitelist in SDKBase ...");
                PatchWhitelist("VRC.SDKBase.Validation.AvatarValidation");
            } catch {
                try {
                    Debug.LogWarning($"{ParamComp.CLogPrefix} SDKBase patch failed, falling back to legacy SDK3 patch ...");
                    PatchWhitelist("VRC.SDK3.Validation.AvatarValidation");
                } catch (Exception e) {
                    Debug.LogError(new Exception($"{ParamComp.CLogPrefix} Local component whitelist patch failed", e));
                }
            }

            // This is purely here because some other addons initialize the vrcsdk whitelist cache for some reason
            try {
                Debug.Log($"{ParamComp.CLogPrefix} Clearing whitelist cache ...");
                var cachedWhitelists = GetFieldInfoFromAnyAssembly("VRC.SDKBase.Validation.ValidationUtils", "_whitelistCache");
                var whitelists = cachedWhitelists.GetValue(null);
                whitelists.GetType().GetMethod("Clear").Invoke(whitelists, Array.Empty<object>());
            } catch (Exception e) {
                Debug.LogError(new Exception($"{ParamComp.CLogPrefix} Failed to clear whitelist cache", e));
            }
        }

        private static void PatchWhitelist(string type) {
            var whitelistField = GetFieldInfoFromAnyAssembly(type, "ComponentTypeWhiteListCommon");
            var whitelist = (string[])whitelistField.GetValue(null);
            ArrayUtility.Add(ref whitelist, CSettingsName);
            whitelistField.SetValue(null, whitelist);
        }

        private static FieldInfo GetFieldInfoFromAnyAssembly(string type, string field) {
            var typeObj = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(type))
                .FirstOrDefault(t => t != null);
            return typeObj.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
#endif
