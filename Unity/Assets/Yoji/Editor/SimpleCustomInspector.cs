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
            var isBreakOn = FindProperty("_BreakOn", props).floatValue > 0;

            foreach (var prop in props)
            {
                if (prop.name == "_BreakOn")
                {
                    editor.ShaderProperty(prop, prop.displayName);
                }
                else if (prop.name.StartsWith("_Break"))
                {
                    if (isBreakOn)
                    {
                        EditorGUI.indentLevel++;
                        editor.ShaderProperty(prop, prop.displayName);
                        EditorGUI.indentLevel--;
                    }
                }
                else
                {
                    editor.ShaderProperty(prop, prop.displayName);
                }
            }
        }
    }
}
