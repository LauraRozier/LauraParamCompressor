#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ParamComp.Editor
{
    public abstract class ParamCompEditorWindow : EditorWindow
    {
        private static readonly Vector2 _windowSize = new(760f, 600f);

        internal UtilParameters _exprParams = new();
        internal VRCExpressionParameters _vrcParameters = null;
        internal AnimatorController _animCtrl = null;
        internal int _boolsPerState = 8;
        internal int _numbersPerState = 1;
        internal ListView _list;

        internal static T GetSizedWindow<T>(string title) where T : ParamCompEditorWindow {
            var wnd = GetWindow(typeof(T), true, title, true) as T;
            wnd.minSize = _windowSize;
            wnd.maxSize = _windowSize;
            return wnd;
        }

        public void OnEnable() =>
            FindAvatars();

        public async void OnHierarchyChange() {
            await Task.Delay(100);
            FindAvatars();
        }

        internal virtual void FindAvatars() { }

        private void BindItem(VisualElement el, int idx) {
            ((ParamField)el).SetValue(_exprParams.Parameters[idx]);
            ((ParamField)el).OnChanged += (val) => {
                var tmp = _exprParams.Parameters[idx];
                tmp.EnableProcessing = val;
                _exprParams.Parameters[idx] = tmp;
            };
        }

        internal void CreateScrollView(VisualElement parent) {
            Box titleBox = new();
            {
                titleBox.style.flexDirection = FlexDirection.Row;
                titleBox.style.paddingBottom = 2;
                titleBox.style.borderBottomWidth = 1;
                titleBox.style.borderBottomColor = Color.grey;

                Label lblName = new("Name");
                lblName.style.width = 400;
                titleBox.Add(lblName);

                Label lblVT = new("Value Type");
                lblVT.style.width = 80;
                titleBox.Add(lblVT);

                Label lblDV = new("Default Value");
                lblDV.style.width = 90;
                titleBox.Add(lblDV);

                Label lblSaved = new("Saved");
                lblSaved.style.width = 50;
                titleBox.Add(lblSaved);

                Label lblEP = new("Enable Processing");
                lblEP.style.width = 110;
                titleBox.Add(lblEP);
            }
            parent.Add(titleBox);

            ScrollView scrollView = new(ScrollViewMode.Vertical);
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
            parent.Add(scrollView);
        }
    }
}
#endif
