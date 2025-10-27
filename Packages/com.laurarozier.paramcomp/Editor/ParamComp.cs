#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public UtilParameterInfo Disable() {
            EnableProcessing = false;
            return this;
        }
    }

    internal class UtilParameters
    {
        public readonly struct NumericParameter
        {
            public readonly string Name;
            public readonly VRCExpressionParameters.ValueType ValueType;

            public NumericParameter(string name, VRCExpressionParameters.ValueType valueType) {
                Name = name;
                ValueType = valueType;
            }
        }

        public const string IsLocalName = "IsLocal";
        public const string SyncPointerName = "Laura/Sync/Ptr";
        public const string SyncTrueName = "Laura/Sync/True";
        public const string SyncDataNumName = "Laura/Sync/DataNum";
        public const string SyncDataBoolName = "Laura/Sync/DataBool";
        private readonly static string[] VRChatParams = new[] {
            IsLocalName, "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight",
            "GestureRightWeight", "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude",
            "Upright", "Grounded", "Seated", "AFK", "TrackingType", "VRMode", "MuteSelf", "InStation",
            "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled", "ScaleModified", "ScaleFactor",
            "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent", "VRCEmote", "VRCFaceBlendH", "VRCFaceBlendV"
        };
        public List<UtilParameterInfo> Parameters { get; } = new();

        public void SetValues(VRCExpressionParameters vrcParams) {
            Parameters.Clear();

            foreach (var param in vrcParams.parameters) {
                // Skip any VRChat default or non-synced parameter
                if (VRChatParams.Contains(param.name) ||
                    SyncPointerName.Equals(param.name, StringComparison.InvariantCulture) ||
                    SyncTrueName.Equals(param.name, StringComparison.InvariantCulture) ||
                    param.name.StartsWith(SyncDataNumName, StringComparison.InvariantCulture) ||
                    param.name.StartsWith(SyncDataBoolName, StringComparison.InvariantCulture) ||
                    !param.networkSynced
                ) continue;

                Parameters.Add(new() {
                    SourceParam = param,
                    EnableProcessing = true
                });
            }
        }

        public (NumericParameter[][], string[][]) GetBatches(int numbersPerState, int boolsPerState) {
            var numbers = Parameters.Where(x =>
                x.EnableProcessing && x.SourceParam.valueType != VRCExpressionParameters.ValueType.Bool
            ).ToList();
            if (numbers.Count <= numbersPerState) numbers.Clear();

            var bools = Parameters.Where(x =>
                x.EnableProcessing && x.SourceParam.valueType == VRCExpressionParameters.ValueType.Bool
            ).ToList();
            if (bools.Count <= boolsPerState) bools.Clear();

            // Size of the pointer + bools per state + 8 * number per state
            var bitsToAdd = 8 +
                (numbers.Count > 0 ? (8 * numbersPerState) : 0) +
                (bools.Count > 0 ? boolsPerState : 0);
            var bitsToRemove = numbers.Concat(bools).Sum(x => VRCExpressionParameters.TypeCost(x.SourceParam.valueType));

            if (bitsToAdd >= bitsToRemove) {
                // Don't optimize if it won't save space
                numbers.Clear();
                bools.Clear();
            }

            if (numbers.Count + bools.Count > 0)
                foreach (var param in numbers.Concat(bools)) {
                    // Disable network sync for all remaining parameters
                    param.SourceParam.networkSynced = false;
                }

            return (
                numbers.Select((x, idx) => (idx, x.SourceParam.name, x.SourceParam.valueType))
                    .GroupBy(x => x.idx / numbersPerState)
                    .Select(g => g.Select(x => new NumericParameter(x.name, x.valueType)).ToArray()).ToArray(),
                bools.Select((x, idx) => (idx, x.SourceParam.name))
                    .GroupBy(x => x.idx / boolsPerState)
                    .Select(g => g.Select(x => x.name).ToArray()).ToArray()
            );
        }
    }

    internal class ParamField : VisualElement
    {
        private readonly Label _lblName;
        private readonly Label _lblValueType;
        private readonly Label _lblDefaultValue;
        private readonly Toggle _tglSaved;
        private readonly Toggle _tglEnableProcessing;

        public event Action<bool> OnChanged;

        public ParamField() {
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

    internal class ParamComp
    {
        private readonly static Vector2 CLayerAnyPos = new(20, -90);
        private readonly static Vector2 CLayerExitPos = new(20, -60);
        private readonly static Vector2 CLayerEntryPos = new(20, -30);
        private readonly static Vector2 CLayerEntrySelectPos = new(0, 60);
        private readonly static Vector2 CCreditPos = new(-300, -140);
        private const string CCreditText = "Laura's Param Compression\nDiscord: LauraRozier";
        private const int CAnimCtrlGridBlockSize = 100;
        private const int CSetStateYPosOffset = CAnimCtrlGridBlockSize;
        private const int CSetStateXPosOffsetIdx0 = CAnimCtrlGridBlockSize * 3;
        private const int CGetStateYPosOffset = 60;
        private const int CExtraFrameXPosOffset = CAnimCtrlGridBlockSize * 3;
        private const int CExtraFrameXPosOffsetLast = -(CAnimCtrlGridBlockSize * 3);
        private const float CStateExitTime = 0.1f;

        internal static void PerformCompression(UtilParameters exprParams, AnimatorController animCtrl, VRCExpressionParameters vrcParameters,
            int numbersPerState, int boolsPerState, bool isBuildTime = false
        ) {
            var (numBatches, boolBatches) = exprParams.GetBatches(numbersPerState, boolsPerState);

            // Skip if we don't have anything to process
            if (numBatches.Length + boolBatches.Length <= 0) return;

            var animCtrlPath = AssetDatabase.GetAssetPath(animCtrl);
            var vrcParametersPath = AssetDatabase.GetAssetPath(vrcParameters);

            if (!isBuildTime)
                BackupOriginals(animCtrlPath, vrcParametersPath);

            bool makeStatesWD = false;

            if (animCtrl.layers.Length > 0)
                makeStatesWD = animCtrl.layers.Any(x => {
                    if (x.stateMachine.states.Length < 1) return false;
                    return x.stateMachine.states.Any(y => y.state.writeDefaultValues);
                });

            var (localMachine, remoteMachine) = AddRequiredObjects(animCtrl, vrcParameters, makeStatesWD, animCtrlPath,
                numBatches.Length > 0, boolBatches.Length > 0, numbersPerState, boolsPerState);
            ProcessParams(animCtrl, localMachine, remoteMachine, makeStatesWD, numBatches, boolBatches);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BackupAsset(string oldPath) {
            var newPath = Path.Combine(
                Path.GetDirectoryName(oldPath),
                $"{Path.GetFileNameWithoutExtension(oldPath)}_backup_{DateTime.Now:yyyy-MM-dd_HH-mm}.{Path.GetExtension(oldPath)}"
            );
            AssetDatabase.CopyAsset(oldPath, newPath);
        }

        private static void BackupOriginals(string animCtrlPath, string vrcParametersPath) {
            BackupAsset(animCtrlPath);
            BackupAsset(vrcParametersPath);
            AssetDatabase.SaveAssets();
        }

        private static (AnimatorStateMachine local, AnimatorStateMachine remote) AddRequiredObjects(AnimatorController animCtrl,
            VRCExpressionParameters vrcParameters, bool makeStatesWD, string animCtrlPath, bool hasNumBatches, bool hasBoolBatches,
            int numbersPerState, int boolsPerState
        ) {
            if (!animCtrl.parameters.Any(x => x.name == UtilParameters.IsLocalName))
                animCtrl.AddParameter(UtilParameters.IsLocalName, AnimatorControllerParameterType.Bool);

            if (!animCtrl.parameters.Any(x => x.name == UtilParameters.SyncTrueName))
                animCtrl.AddParameter(new AnimatorControllerParameter {
                    name = animCtrl.MakeUniqueParameterName(UtilParameters.SyncTrueName),
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                });

            AddIntParameter(animCtrl, vrcParameters, UtilParameters.SyncPointerName);

            if (hasNumBatches)
                for (int i = 0; i < numbersPerState; i++) {
                    AddIntParameter(animCtrl, vrcParameters, $"{UtilParameters.SyncDataNumName}{i}");
                }

            if (hasBoolBatches)
                for (int i = 0; i < boolsPerState; i++) {
                    AddBoolParameter(animCtrl, vrcParameters, $"{UtilParameters.SyncDataBoolName}{i}");
                }

            var layerName = animCtrl.MakeUniqueLayerName("[Laura]CompressedParams");
            AnimatorControllerLayer newLayer = new() {
                name = layerName,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1,
                stateMachine = new() {
                    name = layerName,
                    hideFlags = HideFlags.HideInHierarchy,
                    anyStatePosition = CLayerAnyPos,
                    exitPosition = CLayerExitPos,
                    entryPosition = CLayerEntryPos
                }
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animCtrlPath);
            animCtrl.AddLayer(newLayer);

            var entryState = newLayer.stateMachine.AddState("Entry Selector", CLayerEntrySelectPos);
            entryState.writeDefaultValues = makeStatesWD;
            newLayer.stateMachine.defaultState = entryState;
            AddCredit(newLayer.stateMachine);

            var isLocalIsBool = animCtrl.parameters.First(x => x.name == UtilParameters.IsLocalName).type == AnimatorControllerParameterType.Bool;

            var localMachine = AddStateMachine(newLayer.stateMachine, entryState, false, isLocalIsBool);
            var remoteMachine = AddStateMachine(newLayer.stateMachine, entryState, true, isLocalIsBool);

            return (localMachine, remoteMachine);
        }

        private static void ProcessParams(AnimatorController animCtrl, AnimatorStateMachine localMachine, AnimatorStateMachine remoteMachine,
            bool makeStatesWD, UtilParameters.NumericParameter[][] numBatches, string[][] boolBatches
        ) {
            AnimatorState prevSetState = null;
            Vector2 currentSetPos = new(0, 60),
                    currentGetPos = new(0, 60);
            var stateCount = Math.Max(numBatches.Length, boolBatches.Length);

            for (int i = 0; i < stateCount; i++) {
                var syncIndex = i + 1;
                var (setState, setDriver) = AddState(localMachine, syncIndex, currentSetPos, makeStatesWD, false);
                var (getState, getDriver) = AddState(remoteMachine, syncIndex, currentGetPos, makeStatesWD, true);

                var setStateExtraFrame = localMachine.AddState($"Extra Sync Frame #{syncIndex}",
                    currentSetPos + new Vector2(i == (stateCount - 1) ? CExtraFrameXPosOffsetLast : CExtraFrameXPosOffset, 0));
                setStateExtraFrame.writeDefaultValues = makeStatesWD;
                AddTransition(setState, setStateExtraFrame, true);

                remoteMachine.AddEntryTransition(getState)
                    .AddCondition(AnimatorConditionMode.Equals, syncIndex, UtilParameters.SyncPointerName);

                var exitTrans = getState.AddExitTransition();
                exitTrans.canTransitionToSelf = false;
                exitTrans.hasExitTime = false;
                exitTrans.exitTime = 0f;
                exitTrans.hasFixedDuration = true;
                exitTrans.offset = 0f;
                exitTrans.duration = 0f;
                exitTrans.AddCondition(AnimatorConditionMode.If, 0, UtilParameters.SyncTrueName);

                if (i == 0) {
                    localMachine.defaultState = setState;
                    remoteMachine.defaultState = getState;
                    currentSetPos.x += CSetStateXPosOffsetIdx0;
                } else if (prevSetState != null) {
                    var setExtraFrameTrans = AddTransition(prevSetState, setState, false);
                    setExtraFrameTrans.AddCondition(AnimatorConditionMode.If, 0, UtilParameters.SyncTrueName);
                }

                if (i == (stateCount - 1)) {
                    var setExtraFrameTrans = AddTransition(setStateExtraFrame, localMachine.defaultState, false);
                    setExtraFrameTrans.AddCondition(AnimatorConditionMode.If, 0, UtilParameters.SyncTrueName);
                }

                prevSetState = setStateExtraFrame;

                if (i < numBatches.Count()) {
                    var batch = numBatches[i];

                    for (var batchIdx = 0; batchIdx < batch.Length; batchIdx++) {
                        var item = batch[batchIdx];

                        if (item.ValueType == VRCExpressionParameters.ValueType.Int)
                            AddIntCopy(animCtrl, setDriver, getDriver, item.Name, batchIdx);
                        else // Float
                            AddFloatCopy(animCtrl, setDriver, getDriver, item.Name, batchIdx);
                    }
                }

                if (i < boolBatches.Count()) {
                    var batch = boolBatches[i];

                    for (var batchIdx = 0; batchIdx < batch.Length; batchIdx++) {
                        AddBoolCopy(animCtrl, setDriver, getDriver, batch[batchIdx], batchIdx);
                    }
                }

                currentSetPos.y += CSetStateYPosOffset;
                currentGetPos.y += CGetStateYPosOffset;
            }
        }

        private static AnimatorStateMachine AddStateMachine(AnimatorStateMachine machine, AnimatorState entryState, bool isRemote,
            bool isLocalIsBool
        ) {
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

            if (isLocalIsBool)
                trans.AddCondition(isRemote ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0, UtilParameters.IsLocalName);
            else // Fix for when people convert `IsLocal` to a float
                trans.AddCondition(isRemote ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, isRemote ? 0.992f : 0.008f, UtilParameters.IsLocalName);

            return newMachine;
        }

        private static void AddCredit(AnimatorStateMachine machine) =>
            machine.AddStateMachine(CCreditText, CCreditPos);

        private static AnimatorStateTransition AddTransition(AnimatorState srcState, AnimatorState dstState, bool hasExitTime) {
            var trans = srcState.AddTransition(dstState);
            trans.canTransitionToSelf = false;
            trans.hasExitTime = hasExitTime;
            trans.exitTime = hasExitTime ? CStateExitTime : 0f;
            trans.hasFixedDuration = true;
            trans.offset = 0f;
            trans.duration = 0f;
            return trans;
        }

        private static AnimatorStateTransition AddTransition(AnimatorState srcState, AnimatorStateMachine dstMachine, bool hasExitTime) {
            var trans = srcState.AddTransition(dstMachine);
            trans.canTransitionToSelf = false;
            trans.hasExitTime = hasExitTime;
            trans.exitTime = hasExitTime ? CStateExitTime : 0f;
            trans.hasFixedDuration = true;
            trans.offset = 0f;
            trans.duration = 0f;
            return trans;
        }

        private static void AddIntParameter(AnimatorController animCtrl, VRCExpressionParameters vrcParameters, string name) {
            if (!animCtrl.parameters.Any(x => x.name == name))
                animCtrl.AddParameter(name, AnimatorControllerParameterType.Int);

            if (!vrcParameters.parameters.Any(x => x.name == name)) {
                List<VRCExpressionParameters.Parameter> syncedParamsList = new(vrcParameters.parameters) {new() {
                    name = name,
                    valueType = VRCExpressionParameters.ValueType.Int,
                    saved = false,
                    defaultValue = 0,
                    networkSynced = true
                }};
                vrcParameters.parameters = syncedParamsList.ToArray();
                EditorUtility.SetDirty(vrcParameters);
            }
        }

        private static void AddBoolParameter(AnimatorController animCtrl, VRCExpressionParameters vrcParameters, string name) {
            if (!animCtrl.parameters.Any(x => x.name == name))
                animCtrl.AddParameter(name, AnimatorControllerParameterType.Bool);

            if (!vrcParameters.parameters.Any(x => x.name == name)) {
                List<VRCExpressionParameters.Parameter> syncedParamsList = new(vrcParameters.parameters) {new() {
                    name = name,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    saved = false,
                    defaultValue = 0,
                    networkSynced = true
                }};
                vrcParameters.parameters = syncedParamsList.ToArray();
                EditorUtility.SetDirty(vrcParameters);
            }
        }

        private static (AnimatorState, VRCAvatarParameterDriver) AddState(AnimatorStateMachine machine, int idx, Vector2 pos,
            bool makeStatesWD, bool isRemote
        ) {
            var state = machine.AddState($"{(isRemote ? "Remote Get" : "Local Set")} #{idx}", pos);
            state.writeDefaultValues = makeStatesWD;

            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = false;

            if (!isRemote)
                driver.parameters.Add(new() {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = UtilParameters.SyncPointerName,
                    value = idx
                });

            return (state, driver);
        }

        private static void AddIntCopy(AnimatorController animCtrl, VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver,
            string paramName, int destIdx
        ) {
            if (!animCtrl.parameters.Any(x => x.name == paramName))
                animCtrl.AddParameter(paramName, AnimatorControllerParameterType.Int);

            var syncParamName = $"{UtilParameters.SyncDataNumName}{destIdx}";
            setDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = syncParamName,
                source = paramName
            });
            getDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = paramName,
                source = syncParamName
            });
        }

        private static void AddFloatCopy(AnimatorController animCtrl, VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver,
            string paramName, int destIdx
        ) {
            if (!animCtrl.parameters.Any(x => x.name == paramName))
                animCtrl.AddParameter(paramName, AnimatorControllerParameterType.Float);

            var syncParamName = $"{UtilParameters.SyncDataNumName}{destIdx}";
            setDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = syncParamName,
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
                source = syncParamName,
                sourceMin = 0,
                sourceMax = 254,
                destMin = -1,
                destMax = 1,
                convertRange = true
            });
        }

        private static void AddBoolCopy(AnimatorController animCtrl, VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver,
            string paramName, int destIdx
        ) {
            if (!animCtrl.parameters.Any(x => x.name == paramName))
                animCtrl.AddParameter(paramName, AnimatorControllerParameterType.Bool);

            var syncParamName = $"{UtilParameters.SyncDataBoolName}{destIdx}";
            setDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = syncParamName,
                source = paramName
            });
            getDriver.parameters.Add(new() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = paramName,
                source = syncParamName
            });
        }
    }
}
#endif
