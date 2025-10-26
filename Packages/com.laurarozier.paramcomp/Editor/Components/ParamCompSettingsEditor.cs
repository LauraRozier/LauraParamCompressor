#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ParamComp.Runtime.Components;

namespace ParamComp.Editor.Components
{
    [CustomEditor(typeof(ParamCompSettings))]
    public class ParamCompSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _ExcludedPropertyNamesProperty;
        private SerializedProperty _ExcludedPropertyNamePrefixesProperty;
        private SerializedProperty _ExcludedPropertyNameSuffixesProperty;
        private SerializedProperty _ExcludeVRCFTProperty;
        private SerializedProperty _ExcludeBoolsProperty;
        private SerializedProperty _ExcludeIntsProperty;
        private SerializedProperty _ExcludeFloatsProperty;
        private SerializedProperty _BoolsPerStateProperty;
        private SerializedProperty _NumbersPerStateProperty;

        public void OnEnable() {
            _ExcludedPropertyNamesProperty = serializedObject.FindProperty("ExcludedPropertyNames");
            _ExcludedPropertyNamePrefixesProperty = serializedObject.FindProperty("ExcludedPropertyNamePrefixes");
            _ExcludedPropertyNameSuffixesProperty = serializedObject.FindProperty("ExcludedPropertyNameSuffixes");
            _ExcludeVRCFTProperty = serializedObject.FindProperty("ExcludeVRCFT");
            _ExcludeBoolsProperty = serializedObject.FindProperty("ExcludeBools");
            _ExcludeIntsProperty = serializedObject.FindProperty("ExcludeInts");
            _ExcludeFloatsProperty = serializedObject.FindProperty("ExcludeFloats");
            _BoolsPerStateProperty = serializedObject.FindProperty("BoolsPerState");
            _NumbersPerStateProperty = serializedObject.FindProperty("NumbersPerState");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Laura's Parameter Compressor Settings");
            EditorGUILayout.HelpBox(
                "This component requires VRCFury or Modular Avatar to be in the project, if you wish to test in in the editor.\r\n\r\n" +
                "Without these it'll only work on a test build or avatar upload.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Excluded Property Names");
            EditorGUILayout.PropertyField(_ExcludedPropertyNamesProperty, new GUIContent(string.Empty,
                "These parameters will be excluded from the compressor. Keep in mind that any VRChat default parameters are excluded by default."
            ), true);
            EditorGUILayout.LabelField("Excluded Property Name Prefixes");
            EditorGUILayout.PropertyField(_ExcludedPropertyNamePrefixesProperty, new GUIContent(string.Empty,
                "Parameters STARTING with these strings will be excluded from the compressor. Keep in mind that any VRChat default parameters are excluded by default."
            ), true);
            EditorGUILayout.LabelField("Excluded Property Name Suffixes");
            EditorGUILayout.PropertyField(_ExcludedPropertyNameSuffixesProperty, new GUIContent(string.Empty,
                "Parameters ENDING with these strings will be excluded from the compressor. Keep in mind that any VRChat default parameters are excluded by default."
            ), true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_ExcludeVRCFTProperty, new GUIContent("Exclude VRCFT parameters",
                "Exclude important VRCFaceTracking (Jerry's Templates) parameters from the compressor."
            ), true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_ExcludeBoolsProperty, new GUIContent("Exclude Bools",
                "Exclude all Boolean parameters from the compressor."
            ), true);
            EditorGUILayout.PropertyField(_ExcludeIntsProperty, new GUIContent("Exclude Ints",
                "Exclude all Integer parameters from the compressor."
            ), true);
            EditorGUILayout.PropertyField(_ExcludeFloatsProperty, new GUIContent("Exclude Floats",
                "Exclude all Float parameters from the compressor."
            ), true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_BoolsPerStateProperty, new GUIContent("Bools Per State",
                "Number of Booleans to sync per state."
            ), true);
            EditorGUILayout.PropertyField(_NumbersPerStateProperty, new GUIContent("Numbers Per State",
                "Number of Numbers (Ints/Floats) to sync per state."
            ), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
