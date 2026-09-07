using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace Yoji.Editor
{
    [UnityEditor.AssetImporters.ScriptedImporter(2, "lff")]
    public class FontImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            var sw = new System.Diagnostics.Stopwatch();
            var lines = File.ReadAllLines(ctx.assetPath);
            var font = ScriptableObject.CreateInstance<YojiFont>();
            sw.Start();
            font.BeginEdit();
            Parse(lines, font);
            font.EndEdit();
            sw.Stop();

            ctx.AddObjectToAsset("font", font);
            ctx.SetMainObject(font);

            Debug.Log(sw.ElapsedMilliseconds + "ms");
            EditorUtility.ClearProgressBar();
        }

        void Parse(string[] lines, YojiFont font)
        {
            var buffer = new List<Vector2>();
            int code = -1;
            int npoly = 0;
            int nchar = 0;
            float xmin = float.MaxValue;
            float ymin = float.MaxValue;
            float xmax = float.MinValue;
            float ymax = float.MinValue;
            int codemin = int.MaxValue;
            int codemax = int.MinValue;

            for (int li = 0; li < lines.Length; ++li)
            {
                var line = lines[li];
                if (line.StartsWith("#"))
                {
                    // skip
                    continue;
                }

                if (line.StartsWith("["))
                {
                    if (code >= 0)
                    {
                        int offset = font.PointCount;
                        int length = buffer.Count;
                        foreach (var vec in buffer) {
                            font.AddPoint(vec.x, vec.y);
                        }
                        font.SetGlyph(code, offset, length);
                        code = -1;
                    }

                    if (code < 0)
                    {
                        buffer.Clear();
                        code = ParseCode(line);
                        codemin = Mathf.Min(codemin, code);
                        codemax = Mathf.Max(codemax, code);
                        ++nchar;
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        npoly++;
                        var pts = ParsePoints(line);

                        foreach (var pt in pts)
                        {
                            xmin = Mathf.Min(xmin, pt.x);
                            ymin = Mathf.Min(ymin, pt.y);
                            xmax = Mathf.Max(xmax, pt.x);
                            ymax = Mathf.Max(ymax, pt.y);
                        }

                        for (int i = 0; i < pts.Count - 1; ++i)
                        {
                            buffer.Add(pts[i+0]);
                            buffer.Add(pts[i+1]);
                        }
                    }
                }
                EditorUtility.DisplayProgressBar("Test", "Import...", (float)li / (float)lines.Length);
            }

            Debug.Log("CODE:" + codemin + "-" + codemax);
            Debug.Log("CHAR COUNT:" + nchar);
            Debug.Log("AVE POLYLINE COUNT:" + (float)npoly / (float)nchar);
            Debug.Log("MIN:" + xmin + ", " + ymin);
            Debug.Log("MAX:" + xmax + ", " + ymax);
        }

        List<Vector2> ParsePoints(string text)
        {
            var result = new List<Vector2>();
            var pairs = text.Split(';');
            foreach (var pair in pairs) {
                var xy = pair.Split(',');
                float x = Convert.ToSingle(xy[0]);
                float y = Convert.ToSingle(xy[1]);
                result.Add(new Vector2(x, y));
            }
            return result;
        }

        static int ParseCode(string text)
        {
            int begin = -1;
            for (int i = 0; i < text.Length; ++i)
            {
                var c = text[i];
                if (begin < 0 && c == '[')
                {
                    begin = i;
                }
                if (begin >= 0 && c == ']')
                {
                    return Convert.ToInt32(text.Substring(begin+1, (i-1) - begin), 16);
                }
            }
            return -1;
        }
    }
}

