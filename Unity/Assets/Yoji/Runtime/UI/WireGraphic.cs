#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace Yoji.Runtime.UI
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WireGraphic : Graphic
    {
        protected static int SharedCount = 0;
        protected static Material SharedMaterial = null;
        protected Mesh WorkMesh;

        protected RectTransform? RectTransform;
        protected VertexBuffer? VertexBuffer;
        protected MeshFilter? MeshFilter;
        protected MeshRenderer? MeshRenderer;
        protected Mesh Mesh => WorkMesh ?? (WorkMesh = new Mesh());

#if UNITY_EDITOR
        private void OnBeforeAssemblyReload()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            if (VertexBuffer != null) VertexBuffer.Dispose();
            VertexBuffer = null;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }
#endif
        protected override void OnEnable()
        {
            base.OnEnable();

            RectTransform = RectTransform ?? gameObject.GetComponent<RectTransform>();
            MeshFilter = MeshFilter ?? gameObject.GetComponent<MeshFilter>();
            MeshRenderer = MeshRenderer ?? gameObject.GetComponent<MeshRenderer>();

            if (SharedMaterial == null)
            {
                SharedMaterial = new Material(Shader.Find("Yoji/Simple"));
            }

            MeshFilter.mesh = Mesh;
            MeshRenderer.sharedMaterial = SharedMaterial;
            MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            MeshRenderer.receiveShadows = false;

            Interlocked.Increment(ref SharedCount);
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (VertexBuffer != null) VertexBuffer.Dispose();
            VertexBuffer = null;

            Interlocked.Decrement(ref SharedCount);
            if (SharedCount == 0)
            {
#if UNITY_EDITOR
                DestroyImmediate(SharedMaterial);
#else
                Destroy(SharedMaterial);
#endif
                SharedMaterial = null;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }

    public static class Extensions
    {
        public static Rect MakeRect(this RectTransform rt)
        {
            var c0 = Vector2.zero;
            var c1 = Vector2.one;
            c0.x -= rt.pivot.x;
            c0.y -= rt.pivot.y;
            c1.x -= rt.pivot.x;
            c1.y -= rt.pivot.y;
            var w = rt.rect.width;
            var h = rt.rect.height;
            c0.x *= w;
            c0.y *= h;
            c1.x *= w;
            c1.y *= h;
            return new Rect(c0.x, c0.y, c1.x - c0.x, c1.y - c0.y);
        }
    }
}
