#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ParamComp.Editor
{
    public class ParamCompManual : EditorWindow
    {
        private static readonly Vector2 _windowSize = new(760f, 600f);

        private UtilParameters _exprParams = new();
        private VRCExpressionParameters _vrcParameters = null;
        private AnimatorController _animCtrl = null;
        private int _boolsPerState = 8;
        private int _numbersPerState = 1;
        private ListView _list;

        [MenuItem("Tools/LauraRozier/Parameter Compressor/Manual")]
        public static void ShowWindow() {
            EditorWindow wnd = GetWindow<ParamCompManual>(true, "Parameter Compressor Manual", true);
            wnd.minSize = _windowSize;
            wnd.maxSize = _windowSize;
        }

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
                    _boolsPerState, 8, 32
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

        private void BindItem(VisualElement el, int idx) {
            ((ParamField)el).SetValue(_exprParams.Parameters[idx]);
            ((ParamField)el).OnChanged += (val) => {
                var tmp = _exprParams.Parameters[idx];
                tmp.EnableProcessing = val;
                _exprParams.Parameters[idx] = tmp;
            };
        }
    }
}
#endif