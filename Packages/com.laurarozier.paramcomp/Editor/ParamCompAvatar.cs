#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ParamComp.Editor
{
    public class ParamCompAvatar : ParamCompEditorWindow
    {
        private VRCAvatarDescriptor[] _avatars = null;
        private int _selectedAvatarId = -1;
        private VRCAvatarDescriptor _selectedAvatar = null;
        private string[] _avatarOptions = Array.Empty<string>();
        private VRCExpressionsMenu _exprMenu = null;

        [MenuItem("Tools/LauraRozier/Parameter Compressor/Avatar")]
        public static void ShowWindow() =>
            GetSizedWindow<ParamCompAvatar>("Parameter Compressor Avatar");

        public void CreateGUI() {
            var imguiContainer = new IMGUIContainer(() => {
                EditorGUILayout.Space();

                if (_avatars.Length <= 0) {
                    EditorGUILayout.HelpBox("No avatars in scene!", MessageType.Warning);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                {
                    _selectedAvatarId = EditorGUILayout.Popup("Avatar", _selectedAvatarId, _avatarOptions);
                }
                if (EditorGUI.EndChangeCheck())
                    UpdateSelectedAvatar();

                if (_selectedAvatar == null) return;

                GUI.enabled = false;
                EditorGUILayout.ObjectField("Expression Menu", _exprMenu, typeof(VRCExpressionsMenu), true);
                EditorGUILayout.ObjectField("Expression Parameters", _vrcParameters, typeof(VRCExpressionParameters), true);
                EditorGUILayout.ObjectField("FX Controller", _animCtrl, typeof(RuntimeAnimatorController), true);
                GUI.enabled = true;

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

                if (GUILayout.Button("Compress", GUILayout.ExpandWidth(false))) {
                    ParamComp.PerformCompression(_exprParams, _animCtrl, _vrcParameters, _numbersPerState, _boolsPerState);
                    UpdateSelectedAvatar();
                }

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

        internal override void FindAvatars() {
            var allAvatars = VRC.Tools.FindSceneObjectsOfTypeAll<VRCAvatarDescriptor>();
            // Select only the active avatars
            _avatars = allAvatars.Where(av => null != av && av.gameObject.activeInHierarchy).ToArray();
            _avatarOptions = new string[_avatars.Length];

            if (_avatars.Length <= 0) return;

            for (int i = 0; i < _avatars.Length; i++) {
                _avatarOptions[i] = _avatars[i].name;
            }

            _selectedAvatarId = Array.IndexOf(_avatars, _selectedAvatar);

            if (_selectedAvatarId < 0)
                _selectedAvatarId = 0;

            _selectedAvatar = _avatars[_selectedAvatarId];
            UpdateSelectedAvatar();
        }

        private void UpdateSelectedAvatar() {
            _selectedAvatar = _avatars[_selectedAvatarId];

            if (_selectedAvatar == null) return;

            _exprMenu = _selectedAvatar.expressionsMenu;
            _vrcParameters = _selectedAvatar.expressionParameters;
            _exprParams.SetValues(_vrcParameters);
            _list?.RefreshItems();
            var runtimeCtrl = _selectedAvatar.baseAnimationLayers?.FirstOrDefault(
                    bal => bal.type == VRCAvatarDescriptor.AnimLayerType.FX
                ).animatorController;
            _animCtrl = runtimeCtrl != null
                ? AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(runtimeCtrl))
                : null;
        }
    }
}
#endif