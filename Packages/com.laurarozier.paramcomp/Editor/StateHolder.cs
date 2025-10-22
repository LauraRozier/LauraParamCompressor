#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ParamComp.Editor
{
    internal static class StateHolder
    {
        private static readonly List<Transform> _objStates = new();

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    _objStates.Clear();
            };
        }

        public static bool ShouldProcess(GameObject go) =>
            !Application.isPlaying || !_objStates.Contains(go.transform);

        public static void SetProcessed(GameObject go)
        {
            var goTrans = go.transform;

            if (!_objStates.Contains(goTrans))
                _objStates.Add(goTrans);
        }
    }
}
#endif
