using System;
using UnityEngine;

namespace ParamComp.Runtime.Components
{
    [AddComponentMenu("LauraRozier/ParamComp Settings")]
    public class ParamCompSettings : MonoBehaviour
    {
        public string[] ExcludedPropertyNames = Array.Empty<string>();
        public string[] ExcludedPropertyNamePrefixes = Array.Empty<string>();
        public string[] ExcludedPropertyNameSuffixes = Array.Empty<string>();
        public bool ExcludeVRCFT = true;
        public bool ExcludeBools = false;
        public bool ExcludeInts = false;
        public bool ExcludeFloats = false;
        [Range(8, 32)]
        public int BoolsPerState = 8;
        [Range(1, 8)]
        public int NumbersPerState = 1;
    }
}
