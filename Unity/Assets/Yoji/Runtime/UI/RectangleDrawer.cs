using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Yoji.Runtime.UI
{
    public class RectangleDrawer : WireGraphic
    {
        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            MakeRectangle();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            MakeRectangle();
        }

        void MakeRectangle()
        {
            VertexBuffer = VertexBuffer ?? new VertexBuffer(4);
            var vb = VertexBuffer;
            vb.Clear();
            var rc = RectTransform.MakeRect();
            using (var sm = vb.CreateSubMesh())
            {
                sm.AddLine(new Vector2(rc.xMin, rc.yMin), new Vector2(rc.xMax, rc.yMin), color, color);
                sm.AddLine(new Vector2(rc.xMin, rc.yMax), new Vector2(rc.xMax, rc.yMax), color, color);
                sm.AddLine(new Vector2(rc.xMin, rc.yMin), new Vector2(rc.xMin, rc.yMax), color, color);
                sm.AddLine(new Vector2(rc.xMax, rc.yMin), new Vector2(rc.xMax, rc.yMax), color, color);
            }
            vb.ApplyToMesh(Mesh);
        }
    }
}

