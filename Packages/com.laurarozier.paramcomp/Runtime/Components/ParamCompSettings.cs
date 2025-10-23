using System;
using UnityEngine;

namespace ParamComp.Runtime.Components
{
    [AddComponentMenu("ParamComp/Settings")]
    public class ParamCompSettings : MonoBehaviour
    {
        public string[] ExcludedPropertyNames = Array.Empty<string>();
        public bool ExcludeBools = false;
        public bool ExcludeInts = false;
        public bool ExcludeFloats = false;
    }
}
