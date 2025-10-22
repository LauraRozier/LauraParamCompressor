#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ParamComp.Editor.Components
{
    [AddComponentMenu("ParamComp/Settings")]
    public class ParamCompSettings : MonoBehaviour
    {
        public string[] ExcludedPropertyNames;
        public bool ExcludeBools;
        public bool ExcludeInts;
        public bool ExcludeFloats;
    }

    [CustomEditor(typeof(ParamCompSettings))]
    public class ParamCompSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }
    }
}
#endif
