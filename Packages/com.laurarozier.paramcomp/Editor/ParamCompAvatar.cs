#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ParamComp.Editor
{
    public class ParamCompAvatar : EditorWindow
    {
        private static readonly Vector2 _windowSize = new(760f, 600f);

        private VRCAvatarDescriptor[] _avatars = null;
        private int _selectedAvatarId = -1;
        private VRCAvatarDescriptor _selectedAvatar = null;
        private string[] _avatarOptions = Array.Empty<string>();
        private VRCExpressionsMenu _exprMenu = null;
        private readonly UtilParameters _exprParams = new();
        private VRCExpressionParameters _vrcParameters = null;
        private AnimatorController _animCtrl = null;
        private int _boolsPerState = 8;
        private int _numbersPerState = 1;
        private ListView _list;

        [MenuItem("Tools/LauraRozier/Parameter Compressor/Avatar")]
        public static void ShowWindow() {
            EditorWindow wnd = GetWindow<ParamCompAvatar>(true, "Parameter Compressor Avatar", true);
            wnd.minSize = _windowSize;
            wnd.maxSize = _windowSize;
        }

        private void OnEnable() =>
            FindAvatars();

        private async void OnHierarchyChange() {
            await Task.Delay(100);
            FindAvatars();
        }

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
                if (EditorGUI.EndChangeCheck()) UpdateSelectedAvatar();
                if (_selectedAvatar == null) return;

                GUI.enabled = false;
                EditorGUILayout.ObjectField("Expression Menu", _exprMenu, typeof(VRCExpressionsMenu), true);
                EditorGUILayout.ObjectField("Expression Parameters", _vrcParameters, typeof(VRCExpressionParameters), true);
                EditorGUILayout.ObjectField("FX Controller", _animCtrl, typeof(RuntimeAnimatorController), true);
                GUI.enabled = true;

                EditorGUILayout.Space();
                _boolsPerState = EditorGUILayout.IntSlider(
                    new GUIContent("Bools Per State", "Number of Booleans to sync per state."),
                    _boolsPerState, 8, 32
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

            var titleBox = new Box();
            {
                titleBox.style.flexDirection = FlexDirection.Row;
                titleBox.style.paddingBottom = 2;
                titleBox.style.borderBottomWidth = 1;
                titleBox.style.borderBottomColor = Color.grey;

                var lblName = new Label("Name");
                lblName.style.width = 400;
                titleBox.Add(lblName);

                var lblVT = new Label("Value Type");
                lblVT.style.width = 80;
                titleBox.Add(lblVT);

                var lblDV = new Label("Default Value");
                lblDV.style.width = 90;
                titleBox.Add(lblDV);

                var lblSaved = new Label("Saved");
                lblSaved.style.width = 50;
                titleBox.Add(lblSaved);

                var lblEP = new Label("Enable Processing");
                lblEP.style.width = 110;
                titleBox.Add(lblEP);
            }
            rootVisualElement.Add(titleBox);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            {
                scrollView.style.width = 760;
                scrollView.style.height = 360;
                scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

                _list = new() {
                    showFoldoutHeader = false,
                    showAddRemoveFooter = false,
                    reorderable = false,
                    makeItem = () => new ParamField(),
                    bindItem = BindItem,
                    itemsSource = _exprParams.Parameters
                };
                scrollView.Add(_list);
            }
            rootVisualElement.Add(scrollView);
        }

        private void FindAvatars() {
            var allAvatars = VRC.Tools.FindSceneObjectsOfTypeAll<VRCAvatarDescriptor>();
            // Select only the active avatars
            _avatars = allAvatars.Where(av => null != av && av.gameObject.activeInHierarchy).ToArray();
            _avatarOptions = new string[_avatars.Length];

            if (_avatars.Length <= 0) return;

            for (int i = 0; i < _avatars.Length; i++) {
                _avatarOptions[i] = _avatars[i].name;
            }

            _selectedAvatarId = Array.IndexOf(_avatars, _selectedAvatar);
            if (_selectedAvatarId < 0) _selectedAvatarId = 0;
            _selectedAvatar = _avatars[_selectedAvatarId];
            UpdateSelectedAvatar();
        }

        private void BindItem(VisualElement el, int idx) {
            ((ParamField)el).SetValue(_exprParams.Parameters[idx]);
            ((ParamField)el).OnChanged += (val) => {
                var tmp = _exprParams.Parameters[idx];
                tmp.EnableProcessing = val;
                _exprParams.Parameters[idx] = tmp;
            };
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