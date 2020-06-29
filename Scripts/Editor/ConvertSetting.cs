using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Yoji.Editor
{
    [CreateAssetMenu(menuName = "Yoji/Convert Setting")]
    public class ConvertSetting : ScriptableObject
    {
        [Range(1.0f, 10.0f)]
        public float DefaultLineWidth = 2.0f;

        [Range(0.0f, 1.0f)]
        public float DefaultBackLineDensity = 0.1f;

        [Range(0.0f, 1.0f)]
        public float DefaultEdgeLineDensity = 1.0f;

        [Range(0.0f, 1.0f)]
        public float DefaultFrontLineDensity = 0.5f;

        [Range(0.0f, 180.0f)]
        public float DefaultSmoothAngle = 60.0f;

        [Tooltip("同一の面法線を二本もつ線分を破棄する")]
        public bool DestroyUselessWire = true;
    }
}
