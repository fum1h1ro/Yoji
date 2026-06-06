using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Yoji.Runtime.UI
{
    public class TextDrawer : WireGraphic
    {
        internal class TextInformation
        {
            int FontSize = 16;
            int LetterSpacing = 1;
            int LineSpacing = 1;
            internal float Scale;
            internal int Lines;
            internal Rect Rect;
            internal List<float> Widths = new List<float>();

            internal void Analyze(YojiFont font, string text, int fontsize, int letterspacing, int linespacing)
            {
                FontSize = fontsize;
                LetterSpacing = letterspacing;
                LineSpacing = linespacing;
                Scale = (float)FontSize / (float)font.Size;
                Lines = 1;
                Widths.Clear();
                int nchar = 0;
                float width = 0.0f;
                foreach (var c in text)
                {
                    if (c == '\n')
                    {
                        ++Lines;
                        Flush(ref nchar, ref width);
                    }
                    else
                    {
                        width += font.Get(c).Width * Scale;
                        ++nchar;
                    }
                }
                if (nchar > 0) Flush(ref nchar, ref width);
                width = 0.0f;
                foreach (var w in Widths)
                {
                    width = Mathf.Max(width, w);
                }
                var height = (Lines * font.Height + LineSpacing * (Lines-1)) * Scale;
                Rect = new Rect(0.0f, 0.0f, width, height);
#if false//UNITY_EDITOR
                Debug.Log("Scale:" + Scale);
                Debug.Log("Lines:" + Lines);
                Debug.Log("Rect:" + Rect);
                foreach (var w in Widths)
                {
                    Debug.Log("Widths:" + w);
                }
#endif
            }

            void Flush(ref int nchar, ref float w)
            {
                w += (LetterSpacing * (nchar-1)) * Scale;
                Widths.Add(w);
                w = 0.0f;
                nchar = 0;
            }
        }
        //
        TextInformation Information = new TextInformation();
        Rect Rect;
        public YojiFont Font;
        [TextArea(1, 32)] [SerializeField] public string Text;
        [SerializeField] TextAnchor TextAnchor = TextAnchor.UpperLeft;
        [SerializeField] int FontSize = 16;
        [SerializeField] int LetterSpacing = 1;
        [SerializeField] int LineSpacing = 1;

        protected override void OnEnable()
        {
            base.OnEnable();
            Rebuild();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rebuild();
        }
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Rebuild();
        }
#endif

        public void Rebuild()
        {
            Mesh.Clear();
            if (!Font || string.IsNullOrEmpty(Text))
            {
                return;
            }
            Information.Analyze(Font, Text, FontSize, LetterSpacing, LineSpacing);
            MakeText();
        }

        void MakeText()
        {
            RectTransform = RectTransform ?? gameObject.GetComponent<RectTransform>();

            if (VertexBuffer == null)
            {
                VertexBuffer = new VertexBuffer(128);
                VertexBuffer.AutoExpandScale = 2;
            }
            VertexBuffer.Clear();
            Rect = RectTransform.MakeRect();

            var anchor = CalcAnchor();
            int nline = 1;
            var offset = CalcOffset(anchor, nline);
            var sm = VertexBuffer.CreateSubMesh();
            foreach (var c in Text)
            {
                if (c == '\n')
                {
                    offset = CalcOffset(anchor, ++nline);
                } else
                {
                    AddChar(ref sm, ref offset, c, color);
                }
            }
            sm.Dispose();
            VertexBuffer.ApplyToMesh(Mesh);
        }

        Vector2 CalcOffset(Vector2 anchor, int nline)
        {
            var offset = Vector2.zero;

            switch (TextAnchor)
            {
            default:
            case TextAnchor.UpperLeft:
            case TextAnchor.MiddleLeft:
            case TextAnchor.LowerLeft:
                offset.x = anchor.x;
                break;
            case TextAnchor.UpperCenter:
            case TextAnchor.MiddleCenter:
            case TextAnchor.LowerCenter:
                offset.x = anchor.x - Information.Widths[nline-1] * 0.5f;
                break;
            case TextAnchor.UpperRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.LowerRight:
                offset.x = anchor.x - Information.Widths[nline-1];
                break;
            }

            float textdown = (Font.Size * nline + LineSpacing * (nline-1)) * Information.Scale;

            switch (TextAnchor)
            {
            default:
            case TextAnchor.UpperLeft:
            case TextAnchor.UpperCenter:
            case TextAnchor.UpperRight:
                offset.y = anchor.y - textdown;
                break;
            case TextAnchor.MiddleLeft:
            case TextAnchor.MiddleCenter:
            case TextAnchor.MiddleRight:
                offset.y = anchor.y + Information.Rect.height * 0.5f - textdown;
                break;
            case TextAnchor.LowerLeft:
            case TextAnchor.LowerCenter:
            case TextAnchor.LowerRight:
                offset.y = anchor.y + Information.Rect.height - textdown;
                break;
            }

            return offset;
        }

        Vector2 CalcAnchor()
        {
            var rc = Rect;
            var width = Rect.width;
            var height = Rect.height;

            var left = rc.xMin;
            var center = rc.xMin + width * 0.5f;
            var right = rc.xMax;

            var upper = rc.yMax;
            var middle = rc.yMax - height * 0.5f;
            var lower = rc.yMin;

            switch (TextAnchor)
            {
            default:
            case TextAnchor.UpperLeft:
                return new Vector2(left, upper);
            case TextAnchor.UpperCenter:
                return new Vector2(center, upper);
            case TextAnchor.UpperRight:
                return new Vector2(right, upper);
            case TextAnchor.MiddleLeft:
                return new Vector2(left, middle);
            case TextAnchor.MiddleCenter:
                return new Vector2(center, middle);
            case TextAnchor.MiddleRight:
                return new Vector2(right, middle);
            case TextAnchor.LowerLeft:
                return new Vector2(left, lower);
            case TextAnchor.LowerCenter:
                return new Vector2(center, lower);
            case TextAnchor.LowerRight:
                return new Vector2(right, lower);
            }
        }

        void AddChar(ref VertexBuffer.SubMeshCreator sm, ref Vector2 pos, char c, Color color)
        {
            var table = Font.Get(c);
            var s = Information.Scale;

            for (int i = 0; i < table.Length / 2; ++i)
            {
                var pt0 = Font.Points[table.Offset+i*2+0];
                var pt1 = Font.Points[table.Offset+i*2+1];
                sm.AddLine(new Vector2(pos.x + pt0.x * s, pos.y + pt0.y * s), new Vector2(pos.x + pt1.x * s, pos.y + pt1.y * s), color, color);
            }
            pos.x += ((table.Width + LetterSpacing) * s);
        }
    }
}
