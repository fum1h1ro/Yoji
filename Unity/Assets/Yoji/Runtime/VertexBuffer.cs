#define NATIVE_MESH

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using Unity.Collections;

namespace Yoji.Runtime
{
    public class VertexBuffer : IDisposable
    {
        [System.Flags]
        enum Flag
        {
            NoCull = (1<<0),
            NoSmoothAngle = (1<<1),
            NoFront = (1<<2),
        }

        public struct SubMeshCreator : IDisposable
        {
            private VertexBuffer VB;
            private int IndexStart;
            private int IndexCount;

            internal SubMeshCreator(VertexBuffer vb, int baseIndex)
            {
                VB = vb;
                IndexStart = baseIndex;
                IndexCount = 0;
            }

            public void Dispose()
            {
                VB.EndCreate(IndexStart, IndexCount);
            }

            public void AddLine(Vector3 bgn, Vector3 end, Color col0, Color col1)
            {
                AddLine(bgn, end, Vector3.up, Vector3.up, col0, col1, true, true, true);
            }

            public void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1)
            {
                AddLine(bgn, end, nml0, nml1, col0, col1, true, true, true);
            }

            public void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, bool nosmoothangle, bool nocull, bool nofront)
            {
                VB.AddLine(bgn, end, nml0, nml1, col0, col1, nosmoothangle, nocull, nofront);
                IndexCount += VertexBuffer.NumberOfVerticesOfLine;
            }
        }

        private const int NumberOfVerticesOfLine = 6;
        private int NumLines = 0;
        private List<(int, int)> SubMesh = new List<(int, int)>();
        public int LineCapacity => NumLines;
        public int LineCount => VertexCount / NumberOfVerticesOfLine;
        private int VertexCapacity => NumLines * NumberOfVerticesOfLine;
        public int AutoExpandScale = 0;
        private int VertexCount;

#if !NATIVE_MESH
        private List<Vector3> BeginPos;
        private List<Vector3> Normal0;
        private List<Vector4> Normal1;
        private List<Vector4> Color;
        private List<Vector3> EndPos;
        private List<Vector4> Adjuster;
        private List<int> Indices;
        private int VertexCount => Indices.Count;
#else
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 BeginPos;
            public Vector3 Normal0;
            public Vector4 Normal1;
            public Color32 Color;
            public Vector3 EndPos;
            public Vector4 Adjuster;
        }
        private static VertexAttributeDescriptor[] Layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UInt8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        };
        private NativeArray<uint> Indices;
        private NativeArray<Vertex> Buffer;
#endif

        public VertexBuffer(int nline)
        {
            SetLineCapacity(nline);
        }

        public void Dispose()
        {
#if !NATIVE_MESH
#else
            if (Indices.IsCreated) Indices.Dispose();
            if (Buffer.IsCreated) Buffer.Dispose();
#endif
        }

        public void SetLineCapacity(int nline)
        {
            AllocateBuffer(nline);
        }

        public void Clear()
        {
            SubMesh.Clear();
#if !NATIVE_MESH
            BeginPos.Clear();
            EndPos.Clear();
            Adjuster.Clear();
            Color.Clear();
            Normal0.Clear();
            Normal1.Clear();
            Indices.Clear();
#else
            VertexCount = 0;
#endif
        }

        private void AllocateBuffer(int nline)
        {
            Assert.IsTrue(NumLines < nline);
            int len = nline * NumberOfVerticesOfLine;
#if !NATIVE_MESH
            if (BeginPos == null)
            {
                BeginPos = new List<Vector3>(len);
                EndPos = new List<Vector3>(len);
                Adjuster = new List<Vector4>(len);
                Color = new List<Vector4>(len);
                Normal0 = new List<Vector3>(len);
                Normal1 = new List<Vector4>(len);
                Indices = new List<int>(len);
                Clear();
            }
            else
            {
                BeginPos.Capacity = len;
                EndPos.Capacity = len;
                Adjuster.Capacity = len;
                Color.Capacity = len;
                Normal0.Capacity = len;
                Normal1.Capacity = len;
                Indices.Capacity = len;
            }
#else
            if (!Buffer.IsCreated)
            {
                Indices = new NativeArray<uint>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Buffer = new NativeArray<Vertex>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Clear();
            }
            else
            {
                var newIndices = new NativeArray<uint>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                var newBuffer = new NativeArray<Vertex>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeArray<uint>.Copy(Indices, 0, newIndices, 0, Indices.Length);
                NativeArray<Vertex>.Copy(Buffer, 0, newBuffer, 0, Buffer.Length);
                Indices.Dispose();
                Buffer.Dispose();
                Indices = newIndices;
                Buffer = newBuffer;
            }
#endif
            NumLines = nline;
        }

        public SubMeshCreator CreateSubMesh()
        {
            return new SubMeshCreator(this, VertexCount);
        }
        public void EndCreate(int start, int count)
        {
            SubMesh.Add((start, count));
        }

        protected void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, bool nosmoothangle, bool nocull, bool nofront)
        {
            if (VertexCount >= VertexCapacity)
            {
                if (AutoExpandScale <= 1)
                {
                    Debug.LogWarning("VertexBuffer is full");
                    return;
                }
                else
                {
                    SetLineCapacity(NumLines * AutoExpandScale);
                }
            }

            uint flag = 0;
            flag |= (nocull)? (uint)Flag.NoCull : 0;
            flag |= (nosmoothangle)? (uint)Flag.NoSmoothAngle : 0;
            flag |= (nofront)? (uint)Flag.NoFront : 0;
            float w = (float)flag;
            float z = 0.0f;
            float m = -1.5f;

#if !NATIVE_MESH
            col0.a = 1.0f;
            col1.a = 1.0f;
            // 0
            BeginPos.Add(bgn);
            EndPos.Add(end);
            Adjuster.Add(new Vector4(1, m, z, w));
            Color.Add(col0);
            Normal0.Add(nml0);
            Normal1.Add(nml1);
            Indices.Add(VertexCount++);
            // 1
            BeginPos.Add(end);
            EndPos.Add(bgn);
            Adjuster.Add(new Vector4(-1, m, z, w));
            Color.Add(col1);
            Normal0.Add(nml0);
            Normal1.Add(nml1);
            Indices.Add(VertexCount++);
            // 2
            BeginPos.Add(end);
            EndPos.Add(bgn);
            Adjuster.Add(new Vector4(1, m, z, w));
            Color.Add(col1);
            Normal0.Add(nml0);
            Normal1.Add(nml1);
            Indices.Add(VertexCount++);
            // 3
            BeginPos.Add(bgn);
            EndPos.Add(end);
            Adjuster.Add(new Vector4(1, m, z, w));
            Color.Add(col0);
            Normal0.Add(nml0);
            Normal1.Add(nml1);
            Indices.Add(VertexCount++);
            // 4
            BeginPos.Add(bgn);
            EndPos.Add(end);
            Adjuster.Add(new Vector4(-1, m, z, w));
            Color.Add(col0);
            Normal0.Add(nml0);
            Normal1.Add(nml1);
            Indices.Add(VertexCount++);
            // 5
            BeginPos.Add(end);
            EndPos.Add(bgn);
            Adjuster.Add(new Vector4(1, m, z, w));
            Color.Add(col1);
            Normal0.Add(nml0);
            Normal1.Add(nml1);
            Indices.Add(VertexCount++);
#else
            void AddVertex(ref Vertex v)
            {
                Indices[VertexCount] = (uint)VertexCount;
                Buffer[VertexCount++] = v;
            }

            col0.a = 1.0f;
            col1.a = 1.0f;
            // 0
            var v0 = new Vertex(){
                BeginPos = bgn,
                EndPos = end,
                Adjuster = new Vector4(1, m, z, w),
                Color = col0,
                Normal0 = nml0,
                Normal1 = nml1,
            };
            AddVertex(ref v0);

            var v1 = new Vertex(){
                BeginPos = end,
                EndPos = bgn,
                Adjuster = new Vector4(-1, m, z, w),
                Color = col1,
                Normal0 = nml0,
                Normal1 = nml1,
            };
            AddVertex(ref v1);

            var v2 = new Vertex(){
                BeginPos = end,
                EndPos = bgn,
                Adjuster = new Vector4(1, m, z, w),
                Color = col1,
                Normal0 = nml0,
                Normal1 = nml1,
            };
            AddVertex(ref v2);

            var v3 = new Vertex(){
                BeginPos = bgn,
                EndPos = end,
                Adjuster = new Vector4(1, m, z, w),
                Color = col0,
                Normal0 = nml0,
                Normal1 = nml1,
            };
            AddVertex(ref v3);

            var v4 = new Vertex(){
                BeginPos = bgn,
                EndPos = end,
                Adjuster = new Vector4(-1, m, z, w),
                Color = col0,
                Normal0 = nml0,
                Normal1 = nml1,
            };
            AddVertex(ref v4);

            var v5 = new Vertex(){
                BeginPos = end,
                EndPos = bgn,
                Adjuster = new Vector4(1, m, z, w),
                Color = col1,
                Normal0 = nml0,
                Normal1 = nml1,
            };
            AddVertex(ref v5);
#endif
        }

        public void ApplyToMesh(Mesh mesh)
        {
#if !NATIVE_MESH
            mesh.Clear();
            mesh.SetVertices(BeginPos);
            mesh.SetUVs(0, EndPos);
            mesh.SetUVs(1, Adjuster);
            mesh.SetUVs(2, Color);
            mesh.SetNormals(Normal0);
            mesh.SetTangents(Normal1);

            mesh.subMeshCount = SubMesh.Count;
            for (int i = 0; i < SubMesh.Count; ++i)
            {
                var sm = SubMesh[i];
                mesh.SetIndices(Indices, sm.Item1, sm.Item2, MeshTopology.Triangles, i, true, 0);
            }
#else
            mesh.Clear();

            mesh.SetVertexBufferParams(VertexCount, Layout);
            mesh.SetVertexBufferData(Buffer, 0, 0, VertexCount, 0, MeshUpdateFlags.Default);

            mesh.SetIndexBufferParams(VertexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(Indices, 0, 0, VertexCount, MeshUpdateFlags.Default);

            mesh.subMeshCount = SubMesh.Count;
            for (int i = 0; i < SubMesh.Count; ++i)
            {
                var sm = SubMesh[i];
                var desc = new SubMeshDescriptor(0, 0, MeshTopology.Triangles);
                desc.indexStart = sm.Item1;
                desc.indexCount = sm.Item2;
                mesh.SetSubMesh(i, desc, MeshUpdateFlags.Default);
            }
#endif
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh();
            mesh.name = name;
            ApplyToMesh(mesh);
            return mesh;
        }
    }
}

