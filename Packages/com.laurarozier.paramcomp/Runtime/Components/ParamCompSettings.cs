using System;
using UnityEngine;

namespace ParamComp.Runtime.Components
{
    [AddComponentMenu("LauraRozier/ParamComp Settings")]
    public class ParamCompSettings : MonoBehaviour
    {
        // Parameter Name Exclusions
        public string[] ExcludedPropertyNames = Array.Empty<string>();
        public string[] ExcludedPropertyNamePrefixes = Array.Empty<string>();
        public string[] ExcludedPropertyNameSuffixes = Array.Empty<string>();
        // Package Specific Exclusions
        public bool ExcludeVRCFT = true;
        // Parameter Type Exclusions
        public bool ExcludeBools = false;
        public bool ExcludeInts = false;
        public bool ExcludeFloats = false;

        // Output Settings
        [Range(1, 64)]
        public int BoolsPerState = 8;
        [Range(1, 8)]
        public int NumbersPerState = 1;
    }
}
