using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;



namespace Yoji.Editor
{


    public class SimpleCustomInspector : ShaderGUI
    {




        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            //editor.DrawDefaultInspector();


            //var lineWidthProp = props.First(x => x.name == "_LineWidth");
            //editor.ShaderProperty(lineWidthProp, lineWidthProp.displayName);


            foreach (var prop in props)
            {
                editor.ShaderProperty(prop, prop.displayName);
            }










        }



    }












    public class YojiVectorDrawer : MaterialPropertyDrawer {
        int _index;
        internal YojiVectorDrawer(int idx) {
            _index = idx;
        }
        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor) {
            var vec = prop.vectorValue;
            var val = vec[_index];
            EditorGUI.BeginChangeCheck();
            val = EditorGUI.FloatField(position, label, val);
            vec[_index] = val;
            if (EditorGUI.EndChangeCheck()) {
                prop.vectorValue = vec;
            }
        }
    }





    public class YojiVectorXDrawer : YojiVectorDrawer {
        public YojiVectorXDrawer() : base(0) {}
    }
    public class YojiVectorYDrawer : YojiVectorDrawer {
        public YojiVectorYDrawer() : base(1) {}
    }
    public class YojiVectorZDrawer : YojiVectorDrawer {
        public YojiVectorZDrawer() : base(2) {}
    }
    public class YojiVectorWDrawer : YojiVectorDrawer {
        public YojiVectorWDrawer() : base(3) {}
    }







    public class YojiCosAngleDrawer : MaterialPropertyDrawer
    {
        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var val = Mathf.Acos(prop.floatValue) * Mathf.Rad2Deg;
            EditorGUI.BeginChangeCheck();
            //EditorGUI.showMixedValue = prop.hasMixedValue;
            switch (prop.type)
            {
            case MaterialProperty.PropType.Range:
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
