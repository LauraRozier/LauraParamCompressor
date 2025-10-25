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
    }

    internal class UtilParameters
    {
        public const string IsLocalName = "IsLocal";
        public const string SyncPointerName = "Laura/Sync/Ptr";
        public const string SyncTrueName = "Laura/Sync/True";
        public const string SyncDataNumName = "Laura/Sync/DataNum";
        public const string SyncDataBoolName = "Laura/Sync/DataBool";
        public readonly static string[] VRChatParams = new[] {
            IsLocalName, "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight",
            "GestureRightWeight", "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude",
            "Upright", "Grounded", "Seated", "AFK", "TrackingType", "VRMode", "MuteSelf", "InStation",
            "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled", "ScaleModified", "ScaleFactor",
            "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent", "VRCEmote", "VRCFaceBlendH", "VRCFaceBlendV"
        };
        public List<UtilParameterInfo> Parameters { get; } = new();

        public void SetValues(VRCExpressionParameters vrcParams)
        {
            Parameters.Clear();

            foreach (var param in vrcParams.parameters) {
                // Skip any VRChat default or non-synced parameter
                if (VRChatParams.Contains(param.name) ||
                    SyncPointerName.Equals(param.name, StringComparison.InvariantCulture) ||
                    SyncTrueName.Equals(param.name, StringComparison.InvariantCulture) ||
                    param.name.StartsWith(SyncDataNumName, StringComparison.InvariantCulture) ||
                    param.name.StartsWith(SyncDataBoolName, StringComparison.InvariantCulture) ||
                    !param.networkSynced) continue;

                Parameters.Add(new() {
                    SourceParam = param,
                    EnableProcessing = true
                });
            }
        }

        public bool HasParamsToOptimize() =>
            Parameters.Where(x => x.EnableProcessing).Any();

        public List<UtilParameterInfo> GetBoolParams(int boolsPerState)
        {
            var result = Parameters.Where(x =>
                x.EnableProcessing &&
                x.SourceParam.valueType == VRCExpressionParameters.ValueType.Bool
            ).ToList();
            if (result.Count <= boolsPerState) result.Clear();
            return result;
        }

        public List<UtilParameterInfo> GetNumericParams(int numbersPerState)
        {
            var result = Parameters.Where(x =>
                x.EnableProcessing &&
                x.SourceParam.valueType != VRCExpressionParameters.ValueType.Bool
            ).ToList();
            if (result.Count <= numbersPerState) result.Clear();
            return result;
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

    internal class ParamComp {

        internal static void PerformCompression(UtilParameters exprParams, AnimatorController animCtrl, VRCExpressionParameters vrcParameters,
            bool isBuildTime = false, int boolsPerState = 8, int numbersPerState = 1)
        {
            if (!exprParams.HasParamsToOptimize()) return;

            var boolParams = exprParams.GetBoolParams(boolsPerState);
            var boolBatches = boolParams.Select((x, idx) => (x.SourceParam.name, idx))
                .GroupBy(x => x.idx / boolsPerState)
                .Select(g => g.Select(x => x.name).ToArray()).ToArray();

            var numParams = exprParams.GetNumericParams(numbersPerState);
            var numBatches = numParams.Select((x, idx) => (idx, x.SourceParam.name, x.SourceParam.valueType))
                .GroupBy(x => x.idx / numbersPerState)
                .Select(g => g.Select(x => (x.name, x.valueType)).ToArray()).ToArray();

            var paramsToOptimize = numParams.Concat(boolParams);
            // Size of the pointer + bools per state + 8 * number per state
            var bitsToAdd = 8 + (boolParams.Any() ? boolsPerState : 0) + (numParams.Any() ? (8 * numbersPerState) : 0);
            var bitsToRemove = paramsToOptimize.Sum(p => VRCExpressionParameters.TypeCost(p.SourceParam.valueType));
            if (bitsToAdd >= bitsToRemove) return; // Don't optimize if it won't save space

            var animCtrlPath = AssetDatabase.GetAssetPath(animCtrl);
            var vrcParametersPath = AssetDatabase.GetAssetPath(vrcParameters);

            if (!isBuildTime)
                BackupOriginals(animCtrlPath, vrcParametersPath);

            var (localMachine, remoteMachine) = AddRequiredObjects(animCtrl, vrcParameters, animCtrlPath, numParams.Any(), boolParams.Any(), boolsPerState, numbersPerState);
            ProcessParams(animCtrl, localMachine, remoteMachine, boolBatches, numBatches, paramsToOptimize);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BackupAsset(string oldPath)
        {
            var newPath = Path.Combine(
                Path.GetDirectoryName(oldPath),
                $"{Path.GetFileNameWithoutExtension(oldPath)}_backup_{DateTime.Now:yyyy-MM-dd_HH-mm}.{Path.GetExtension(oldPath)}"
            );
            AssetDatabase.CopyAsset(oldPath, newPath);
        }

        private static void BackupOriginals(string animCtrlPath, string vrcParametersPath)
        {
            BackupAsset(animCtrlPath);
            BackupAsset(vrcParametersPath);
            AssetDatabase.SaveAssets();
        }

        private static (AnimatorStateMachine local, AnimatorStateMachine remote) AddRequiredObjects(AnimatorController animCtrl, VRCExpressionParameters vrcParameters,
            string animCtrlPath, bool hasNumBatches, bool hasBoolBatches, int boolsPerState, int numbersPerState)
        {
            if (!animCtrl.parameters.Any(x => x.name == UtilParameters.IsLocalName))
                animCtrl.AddParameter(UtilParameters.IsLocalName, AnimatorControllerParameterType.Bool);

            if (!animCtrl.parameters.Any(x => x.name == UtilParameters.SyncTrueName))
                animCtrl.AddParameter(new AnimatorControllerParameter
                {
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
                    entryPosition = new(20, -30),
                    exitPosition = new(20, -60),
                    anyStatePosition = new(20, -90)
                }
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animCtrlPath);
            animCtrl.AddLayer(newLayer);

            var entryState = newLayer.stateMachine.AddState("Entry Selector", new(0, 60));
            entryState.writeDefaultValues = false;
            newLayer.stateMachine.defaultState = entryState;
            AddCredit(newLayer.stateMachine);

            var isLocalIsBool = animCtrl.parameters.First(x => x.name == UtilParameters.IsLocalName).type == AnimatorControllerParameterType.Bool;

            var localMachine = AddStateMachine(newLayer.stateMachine, entryState, false, isLocalIsBool);
            var remoteMachine = AddStateMachine(newLayer.stateMachine, entryState, true, isLocalIsBool);

            return (localMachine, remoteMachine);
        }

        private static void ProcessParams(AnimatorController animCtrl, AnimatorStateMachine localMachine, AnimatorStateMachine remoteMachine,
            string[][] boolBatches, (string,VRCExpressionParameters.ValueType)[][] numBatches, IEnumerable<UtilParameterInfo> paramsToProcess
        ) {
            foreach (var param in paramsToProcess) {
                param.SourceParam.networkSynced = false;
            }

            AnimatorState prevSetState = null;
            Vector2 currentSetPos = new(0, 60), currentGetPos = new(0, 60);
            var stepCount = Math.Max(boolBatches.Count(), numBatches.Count());

            for (int i = 0; i < stepCount; i++) {
                if (i == stepCount-1) currentSetPos.x -= 200;
                var syncIndex = i + 1;
                var (setState, setDriver) = AddState(localMachine, syncIndex, currentSetPos, false);
                var (getState, getDriver) = AddState(remoteMachine, syncIndex, currentGetPos, true);

                remoteMachine.AddEntryTransition(getState)
                    .AddCondition(AnimatorConditionMode.Equals, syncIndex, UtilParameters.SyncPointerName);

                var exitTrans = getState.AddExitTransition();
                exitTrans.hasExitTime = false;
                exitTrans.exitTime = 0f;
                exitTrans.hasFixedDuration = true;
                exitTrans.duration = 0f;
                exitTrans.offset = 0f;
                exitTrans.AddCondition(AnimatorConditionMode.If, 0, UtilParameters.SyncTrueName);

                if (i == 0) {
                    localMachine.defaultState = setState;
                    remoteMachine.defaultState = getState;
                    currentSetPos.x += 200;
                } else if (prevSetState != null) {
                    AddTransition(prevSetState, setState, true);
                }

                if (i == stepCount-1) AddTransition(setState, localMachine.defaultState, true);
                prevSetState = setState;

                if (i < numBatches.Count()) {
                    var batch = numBatches[i];

                    for (var batchIdx = 0; batchIdx < batch.Length; batchIdx++) {
                        var (name, type) = batch[batchIdx];

                        if (type == VRCExpressionParameters.ValueType.Int)
                            AddIntCopy(animCtrl, setDriver, getDriver, name, batchIdx);
                        else
                            AddFloatCopy(animCtrl, setDriver, getDriver, name, batchIdx);
                    }
                }

                if (i < boolBatches.Count()) {
                    var batch = boolBatches[i];

                    for (var batchIdx = 0; batchIdx < batch.Length; batchIdx++) {
                        AddBoolCopy(animCtrl, setDriver, getDriver, batch[batchIdx], batchIdx);
                    }
                }

                currentSetPos.y += 100;
                currentGetPos.y += 60;
            }
        }

        private static AnimatorStateMachine AddStateMachine(AnimatorStateMachine machine, AnimatorState entryState, bool isRemote, bool isLocalIsBool)
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

            if (isLocalIsBool)
                trans.AddCondition(isRemote ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0, UtilParameters.IsLocalName);
            else // Fix for VRCFury fuckery, converting IsLocal to a float
                trans.AddCondition(isRemote ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, isRemote ? 0.992f : 0.008f, UtilParameters.IsLocalName);

            return newMachine;
        }

        private static void AddCredit(AnimatorStateMachine machine) =>
            machine.AddStateMachine("Laura's Param Compression\nDiscord: LauraRozier", new(-300, -140));

        private static AnimatorStateTransition AddTransition(AnimatorState srcState, AnimatorState dstState, bool hasExitTime)
        {
            var trans = srcState.AddTransition(dstState);
            trans.hasExitTime = hasExitTime;
            trans.exitTime = hasExitTime ? 0.5f : 0f;
            trans.hasFixedDuration = true;
            trans.offset = 0f;
            trans.duration = 0f;
            return trans;
        }

        private static AnimatorStateTransition AddTransition(AnimatorState srcState, AnimatorStateMachine  dstMachine, bool hasExitTime)
        {
            var trans = srcState.AddTransition(dstMachine);
            trans.hasExitTime = hasExitTime;
            trans.exitTime = hasExitTime ? 0.5f : 0f;
            trans.hasFixedDuration = true;
            trans.offset = 0f;
            trans.duration = 0f;
            return trans;
        }

        private static void AddIntParameter(AnimatorController animCtrl, VRCExpressionParameters vrcParameters, string name)
        {
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

        private static void AddBoolParameter(AnimatorController animCtrl, VRCExpressionParameters vrcParameters, string name)
        {
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

        private static (AnimatorState, VRCAvatarParameterDriver) AddState(AnimatorStateMachine machine, int idx, Vector2 pos, bool isRemote)
        {
            var state = isRemote
                ? machine.AddState($"Remote Get #{idx}", pos)
                : machine.AddState($"Local Set #{idx}", pos);
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

        private static void AddIntCopy(AnimatorController animCtrl, VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver, string paramName, int destIdx)
        {
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

        private static void AddFloatCopy(AnimatorController animCtrl, VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver, string paramName, int destIdx)
        {
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

        private static void AddBoolCopy(AnimatorController animCtrl, VRCAvatarParameterDriver setDriver, VRCAvatarParameterDriver getDriver, string paramName, int destIdx)
        {
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
