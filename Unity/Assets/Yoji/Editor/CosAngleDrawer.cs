using UnityEngine;
using UnityEditor;

namespace Yoji.Editor
{
    public class CosAngleDrawer : MaterialPropertyDrawer
    {
        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var val = Mathf.Acos(prop.floatValue) * Mathf.Rad2Deg;
            EditorGUI.BeginChangeCheck();
            switch (prop.propertyType)
            {
            case UnityEngine.Rendering.ShaderPropertyType.Range:
                var old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0;
                val = EditorGUI.Slider(position, label, val, prop.rangeLimits.x, prop.rangeLimits.y);
                EditorGUIUtility.labelWidth = old;
                break;
            default:
                val = EditorGUI.FloatField(position, label, val);
                break;
            }
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = ConvertAngle(val);
            }
        }

        public static float ConvertAngle(float angle)
        {
            return Mathf.Cos(angle * Mathf.Deg2Rad);
        }
    }
}
