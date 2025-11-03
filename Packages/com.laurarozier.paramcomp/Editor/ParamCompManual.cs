#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ParamComp.Editor
{
    public class ParamCompManual : ParamCompEditorWindow
    {
        [MenuItem("Tools/LauraRozier/Parameter Compressor/Manual")]
        public static void ShowWindow() =>
            GetSizedWindow<ParamCompManual>("Parameter Compressor Manual");

        public void CreateGUI() {
            IMGUIContainer imguiContainer = new(() => {
                EditorGUILayout.Space();
                EditorGUI.BeginChangeCheck();
                {
                    _vrcParameters = (VRCExpressionParameters)EditorGUILayout.ObjectField("Expression Parameters", _vrcParameters, typeof(VRCExpressionParameters), true);
                    _animCtrl = (AnimatorController)EditorGUILayout.ObjectField("FX Controller", _animCtrl, typeof(AnimatorController), true);
                }
                if (EditorGUI.EndChangeCheck()) {
                    _exprParams.SetValues(_vrcParameters);
                    _list?.RefreshItems();
                }

                EditorGUILayout.Space();
                _boolsPerState = EditorGUILayout.IntSlider(
                    new GUIContent("Bools Per State", "Number of Booleans to sync per state."),
                    _boolsPerState, 1, 64
                );
                _numbersPerState = EditorGUILayout.IntSlider(
                    new GUIContent("Numbers Per State", "Number of Numbers (Ints/Floats) to sync per state."),
                    _numbersPerState, 1, 8
                );
                EditorGUILayout.Space();

                GUI.enabled = _vrcParameters != null && _animCtrl != null;

                if (GUILayout.Button("Compress", GUILayout.ExpandWidth(false)))
                    ParamComp.PerformCompression(_exprParams, _animCtrl, _vrcParameters, _numbersPerState, _boolsPerState);

                GUI.enabled = true;

                EditorGUILayout.HelpBox(
                    "Only non-VRChat and Synced parameters are shown.\n" +
                    "This tool also makes backup of the original FX controller and VRChat Parameters, you can find them at the same location as the sources.\n\n" +
                    "Keep in mind that this tool will also skip any compression run where there are no bit-count savings!",
                    MessageType.Info
                );
                EditorGUILayout.Space();
            });
            rootVisualElement.Add(imguiContainer);
            CreateScrollView(rootVisualElement);
        }
    }
}
#endif