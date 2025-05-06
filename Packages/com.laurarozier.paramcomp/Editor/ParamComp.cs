#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace ParamComp.Editor
{
    internal struct UtilParameterInfo
    {
        public VRCExpressionParameters.Parameter SourceParam;
        public bool EnableProcessing;
    }

    internal class UtilParameters
    {
        public readonly static string[] VRChatParams = new[] {
            "IsLocal", "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight",
            "GestureRightWeight", "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude",
            "Upright", "Grounded", "Seated", "AFK", "TrackingType", "VRMode", "MuteSelf", "InStation",
            "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled", "ScaleModified", "ScaleFactor",
            "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent", "VRCEmote", "VRCFaceBlendH", "VRCFaceBlendV"
        };
        public const int BoolBatchSize = 8;
        public const string SyncPointerName = "Laura/Sync/Ptr";
        public const string SyncDataNumName = "Laura/Sync/DataNum";
        public readonly static string[] SyncDataBoolNames = new[] {
            "Laura/Sync/DataBool0",
            "Laura/Sync/DataBool1",
            "Laura/Sync/DataBool2",
            "Laura/Sync/DataBool3",
            "Laura/Sync/DataBool4",
            "Laura/Sync/DataBool5",
            "Laura/Sync/DataBool6",
            "Laura/Sync/DataBool7",
        };
        public List<UtilParameterInfo> Parameters { get; } = new();

        public void SetValues(VRCExpressionParameters vrcParams)
        {
            Parameters.Clear();

            foreach (var param in vrcParams.parameters) {
                // Skip any VRChat default or non-synced parameter
                if (VRChatParams.Contains(param.name) ||
                    SyncDataBoolNames.Contains(param.name) ||
                    SyncPointerName.Equals(param.name, StringComparison.InvariantCulture) ||
                    SyncDataNumName.Equals(param.name, StringComparison.InvariantCulture) ||
                    !param.networkSynced) continue;

                Parameters.Add(new() {
                    SourceParam = param,
                    EnableProcessing = true
                });
            }
        }

        public bool HasParamsToOptimize() =>
            Parameters.Where(x => x.EnableProcessing).Any();

        public List<UtilParameterInfo> GetBoolParams()
        {
            var result = Parameters.Where(x =>
                x.EnableProcessing &&
                x.SourceParam.valueType == VRCExpressionParameters.ValueType.Bool
            ).ToList();
            if (result.Count <= BoolBatchSize) result.Clear();
            return result;
        }

        public List<UtilParameterInfo> GetNumericParams() =>
            Parameters.Where(x =>
                x.EnableProcessing &&
                x.SourceParam.valueType != VRCExpressionParameters.ValueType.Bool
            ).ToList();
    }

    internal class ParamField : VisualElement
    {
        private readonly Label _lblName;
        private readonly Label _lblValueType;
        private readonly Label _lblDefaultValue;
        private readonly Toggle _tglSaved;
        private readonly Toggle _tglEnableProcessing;

        public event Action<bool> OnChanged;

        public ParamField()
        {
            var valueRow = new Box();
            valueRow.style.flexDirection = FlexDirection.Row;
            {
                _lblName = new Label();
                _lblName.style.width = 400;
                valueRow.Add(_lblName);

                _lblValueType = new Label();
                _lblValueType.style.width = 80;
                valueRow.Add(_lblValueType);

                _lblDefaultValue = new Label();
                _lblDefaultValue.style.width = 90;
                valueRow.Add(_lblDefaultValue);

                _tglSaved = new Toggle();
                _tglSaved.style.width = 50;
                _tglSaved.SetEnabled(false);
                valueRow.Add(_tglSaved);

                _tglEnableProcessing = new Toggle();
                _tglEnableProcessing.style.width = 110;
                _tglEnableProcessing.RegisterValueChangedCallback(evt => OnChanged?.Invoke(evt.newValue));
                valueRow.Add(_tglEnableProcessing);
            }
            Add(valueRow);
        }

        public void SetValue(UtilParameterInfo val) {
            _lblName.text = val.SourceParam.name;
            _lblValueType.text = val.SourceParam.valueType.ToString();
            _lblDefaultValue.text = val.SourceParam.defaultValue.ToString();
            _tglSaved.value = val.SourceParam.saved;
            _tglEnableProcessing.SetValueWithoutNotify(val.EnableProcessing);
        }
    }

    public class ParamComp : EditorWindow
    {
        private static readonly Vector2 _windowSize = new Vector2(760f, 600f);

        private VRCAvatarDescriptor[] _avatars = null;
        private int _selectedAvatarId = -1;
        private VRCAvatarDescriptor _selectedAvatar = null;
        private string[] _avatarOptions = Array.Empty<string>();
        private VRCExpressionsMenu _exprMenu = null;
        private UtilParameters _exprParams = new();
        private VRCExpressionParameters _vrcParameters = null;
        private AnimatorController _animCtrl = null;
        private ListView _list;
        private Motion _stateMotion;

        [MenuItem("Tools/LauraRozier/Parameter Compressor")]
        public static void ShowWindow() {
            EditorWindow wnd = GetWindow<ParamComp>(true, "Parameter Compressor", true);
            wnd.minSize = _windowSize;
            wnd.maxSize = _windowSize;
        }

        private void OnEnable() => FindAvatars();

        private async void OnHierarchyChange()
        {
            await Task.Delay(100);
            FindAvatars();
        }

        public void CreateGUI()
        {
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
                if (GUILayout.Button("Compress", GUILayout.ExpandWidth(false))) PerformCompression();
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
                scrollView.style.height = 420;
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

        private void FindAvatars()
        {
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

        private void BackupAsset(string oldPath)
        {
            var newPath = Path.Combine(
                Path.GetDirectoryName(oldPath),
                $"{Path.GetFileNameWithoutExtension(oldPath)}_backup_{DateTime.Now:yyyy-MM-dd_HH-mm}.{Path.GetExtension(oldPath)}"
            );
            AssetDatabase.CopyAsset(oldPath, newPath);
        }

        private void PerformCompression()
        {
            if (!_exprParams.HasParamsToOptimize()) return;

            var boolParams = _exprParams.GetBoolParams();
            var numParams = _exprParams.GetNumericParams();
            var boolBatches = boolParams.Select((x, idx) => (x.SourceParam.name, idx))
                .GroupBy(x => x.idx / UtilParameters.BoolBatchSize)
                .Select(g => g.Select(x => x.name).ToArray()).ToList();
            var paramsToOptimize = numParams.Concat(boolParams);
            var bitsToAdd = 8 + (numParams.Any() ? 8 : 0) + (boolParams.Any() ? UtilParameters.BoolBatchSize : 0);
            var bitsToRemove = paramsToOptimize.Sum(p => VRCExpressionParameters.TypeCost(p.SourceParam.valueType));
            if (bitsToAdd >= bitsToRemove) return; // Don't optimize if it won't save space

            var animCtrlPath = AssetDatabase.GetAssetPath(_animCtrl);
            var vrcParametersPath = AssetDatabase.GetAssetPath(_vrcParameters);
            _stateMotion = AssetDatabase.LoadAssetAtPath<Motion>("Packages/com.laurarozier.paramcomp/Resources/TenFrame.anim");

            BackupOriginals(animCtrlPath, vrcParametersPath);
            var (localMachine, remoteMachine) = AddRequiredObjects(animCtrlPath, numParams.Any());
            ProcessParams(localMachine, remoteMachine, boolBatches, numParams.Select(x => (x.SourceParam.name, x.SourceParam.valueType)).ToArray(), paramsToOptimize);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UpdateSelectedAvatar();
        }

        private void BackupOriginals(string animCtrlPath, string vrcParametersPath)
        {
            BackupAsset(animCtrlPath);
            BackupAsset(vrcParametersPath);
            AssetDatabase.SaveAssets();
        }

        private (AnimatorStateMachine local, AnimatorStateMachine remote) AddRequiredObjects(string animCtrlPath, bool hasNumParams)
        {
            if (!_animCtrl.parameters.Any(x => x.name == "IsLocal"))
                _animCtrl.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);

            AddIntParameter(UtilParameters.SyncPointerName);
            if (hasNumParams) AddIntParameter(UtilParameters.SyncDataNumName);

            for (int i = 0; i < UtilParameters.BoolBatchSize; i++) {
                AddBoolParameter(UtilParameters.SyncDataBoolNames[i]);
            }

            var layerName = _animCtrl.MakeUniqueLayerName("[Laura]CompressedParams");
            AnimatorControllerLayer newLayer = new() {
                name = layerName,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1,
                stateMachine = new() {
                    name = layerName,
                    hideFlags = HideFlags.HideInHierarchy,
                    entryPosition = new(20, -30),
                    exitPosition = new(20, -60),
                    anyStatePosition = new(20, -90)
                }
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animCtrlPath);
            _animCtrl.AddLayer(newLayer);

            var entryState = newLayer.stateMachine.AddState("Entry Selector", new(0, 60));
            entryState.motion = _stateMotion;
            entryState.writeDefaultValues = false;
            newLayer.stateMachine.defaultState = entryState;
            AddCredit(newLayer.stateMachine);

            var localMachine = AddStateMachine(newLayer.stateMachine, entryState, false);
            var remoteMachine = AddStateMachine(newLayer.stateMachine, entryState, true);

            return (localMachine, remoteMachine);
        }

        private void ProcessParams(AnimatorStateMachine localMachine, AnimatorStateMachine remoteMachine,
            List<string[]> boolBatches, (string,VRCExpressionParameters.ValueType)[] numParams, IEnumerable<UtilParameterInfo> paramsToProcess
        ) {
            foreach (var param in paramsToProcess) {
                param.SourceParam.networkSynced = false;
            }

            AnimatorState prevSetState = null;
            Vector2 currentSetPos = new(0, 60), currentGetPos = new(0, 60);
            var stepCount = Math.Max(boolBatches.Count(), numParams.Length);

            for (int i = 0; i < stepCount; i++) {
                if (i == stepCount-1) currentSetPos.x -= 200;
                var syncIndex = i + 1;
                var (setState, setDriver) = AddState(localMachine, syncIndex, currentSetPos, false);
                var (getState, getDriver) = AddState(remoteMachine, syncIndex, currentGetPos, true);

                remoteMachine.AddEntryTransition(getState)
                    .AddCondition(AnimatorConditionMode.Equals, syncIndex, UtilParameters.SyncPointerName);

                var outTrans = getState.AddExitTransition();
                outTrans.hasExitTime = true;
                outTrans.exitTime = 0f;
                outTrans.hasFixedDuration = true;
                outTrans.duration = 0f;
                outTrans.offset = 0f;

                if (i == 0) {
                    localMachine.defaultState = setState;
                    remoteMachine.defaultState = getState;
                    currentSetPos.x += 200;
                } else if (prevSetState != null) {
                    AddTransition(prevSetState, setState, true);
                }

                if (i == stepCount-1) AddTransition(setState, localMachine.defaultState, true);
                prevSetState = setState;

                if (i < numParams.Length) {
                    var (name, type) = numParams[i];

                    if (type == VRCExpressionParameters.ValueType.Int)
                        AddIntCopy(setDriver, getDriver, name);
                    else
                        AddFloatCopy(setDriver, getDriver, name);
                }

                if (i < boolBatches.Count()) {
                    var batch = boolBatches[i];

                    for (var batchIdx = 0; batchIdx < batch.Length; batchIdx++) {
                        AddBoolCopy(setDriver, getDriver, batch[batchIdx], batchIdx);
                    }
                }

                currentSetPos.y += 100;
                currentGetPos.y += 60;
            }
        }

        private AnimatorStateMachine AddStateMachine(AnimatorStateMachine machine, AnimatorState entryState, bool isRemote)
        {
            AnimatorStateMachine newMachine;

            if (isRemote) {
                newMachine = machine.AddStateMachine("Remote User (Get)", new(200, 160));
                newMachine.entryPosition = new(-260, -30);
                newMachine.exitPosition = new(300, -30);
                newMachine.anyStatePosition = new(20, -30);
                newMachine.parentStateMachinePosition = new(0, -70);
            } else {
                newMachine = machine.AddStateMachine("Local User (Set)", new(-200, 160));
                newMachine.entryPosition = new(20, -30);
                newMachine.exitPosition = new(20, -60);
                newMachine.anyStatePosition = new(20, -90);
                newMachine.parentStateMachinePosition = new(0, -130);
            }

            AddCredit(newMachine);
            var trans = AddTransition(entryState, newMachine, false);
            trans.AddCondition(isRemote ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0, "IsLocal");
            return newMachine;
        }

        private void AddCredit(AnimatorStateMachine machine) =>
            machine.AddStateMachine("Laura's Param Compression\nDiscord: LauraRozier", new(-300, -140));

        private AnimatorStateTransition  AddTransition(AnimatorState srcState, AnimatorState dstState, bool hasExitTime, float exitTime = 1f)
        {
            var trans = srcState.AddTransition(dstState);
            trans.hasExitTime = hasExitTime;
            trans.exitTime = hasExitTime ? exitTime : 0f;
            trans.hasFixedDuration = true;
            trans.offset = 0f;
            trans.duration = 0f;
            return trans;
        }

        private AnimatorStateTransition  AddTransition(AnimatorState srcState, AnimatorStateMachine  dstMachine, bool hasExitTime, float exitTime = 1f)
        {
            var trans = srcState.AddTransition(dstMachine);
            trans.hasExitTime = hasExitTime;
            trans.exitTime = hasExitTime ? exitTime : 0f;
            trans.hasFixedDuration = true;
            trans.offset = 0f;
            trans.duration = 0f;
            return trans;
        }

        private void AddIntParameter(string name)
        {
            _animCtrl.AddParameter(name, AnimatorControllerParameterType.Int);
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(_vrcParameters.parameters) {
                new() {
                    name = name,
                    valueType = VRCExpressionParameters.ValueType.Int,
                    saved = false,
                    defaultValue = 0,
                    networkSynced = true
                }
            };
            _vrcParameters.parameters = syncedParamsList.ToArray();
            EditorUtility.SetDirty(_vrcParameters);
        }

        private void AddBoolParameter(string name)
        {
            _animCtrl.AddParameter(name, AnimatorControllerParameterType.Bool);
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(_vrcParameters.parameters) {
                new() {
                    name = name,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    saved = false,
                    defaultValue = 0,
                    networkSynced = true
                }
            };
            _vrcParameters.parameters = syncedParamsList.ToArray();
            EditorUtility.SetDirty(_vrcParameters);
        }

        private (AnimatorState, VRCAvatarParameterDriver) AddState(AnimatorStateMachine machine, int idx, Vector2 pos, bool isRemote)
        {
            var state = isRemote
                ? machine.AddState($"Remote Get #{idx}", pos)
                : machine.AddState($"Local Set #{idx}", pos);
            state.motion = _stateMotion;
            state.writeDefaultValues = false;

            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = false;

            if (!isRemote) driver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = UtilParameters.SyncPointerName,
                value = idx
            });

            return (state, driver);
        }

        private void AddIntCopy(VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver, string paramName)
        {
            if (!_animCtrl.parameters.Any(x => x.name == paramName))
                _animCtrl.AddParameter(paramName, AnimatorControllerParameterType.Int);

            setDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = UtilParameters.SyncDataNumName,
                source = paramName
            });
            getDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = paramName,
                source = UtilParameters.SyncDataNumName
            });
        }

        private void AddFloatCopy(VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver, string paramName)
        {
            if (!_animCtrl.parameters.Any(x => x.name == paramName))
                _animCtrl.AddParameter(paramName, AnimatorControllerParameterType.Float);

            setDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = UtilParameters.SyncDataNumName,
                source = paramName,
                sourceMin = -1,
                sourceMax = 1,
                destMin = 0,
                destMax = 254,
                convertRange = true
            });
            getDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = paramName,
                source = UtilParameters.SyncDataNumName,
                sourceMin = 0,
                sourceMax = 254,
                destMin = -1,
                destMax = 1,
                convertRange = true
            });
        }

        private void AddBoolCopy(VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver, string paramName, int destIdx)
        {
            if (!_animCtrl.parameters.Any(x => x.name == paramName))
                _animCtrl.AddParameter(paramName, AnimatorControllerParameterType.Bool);

            setDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = UtilParameters.SyncDataBoolNames[destIdx],
                source = paramName
            });
            getDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = paramName,
                source = UtilParameters.SyncDataBoolNames[destIdx]
            });
        }
    }
}
#endif
