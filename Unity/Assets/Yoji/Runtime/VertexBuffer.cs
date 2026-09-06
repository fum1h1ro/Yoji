using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Pool;

namespace Yoji
{
    public class VertexBuffer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex
        {
            public Vector3 BeginPos;
            public Vector3 Normal0;
            public Vector4 Normal1;
            public Color32 Color;
            public Vector3 EndPos;
            public Vector4 Adjuster;
        }
        private readonly static VertexAttributeDescriptor[] Layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UInt8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        };

        public struct SubMeshCreator : IDisposable
        {
            private VertexBuffer _vb;
            private int _indexStart;
            private int _indexCount;

            internal SubMeshCreator(VertexBuffer vb, int baseIndex)
            {
                _vb = vb;
                _indexStart = baseIndex;
                _indexCount = 0;
            }

            public void Dispose()
            {
                _vb.CreateSubMeshEnd(_indexStart, _indexCount);
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
                _vb.AddLine(bgn, end, nml0, nml1, col0, col1, nosmoothangle, nocull, nofront);
                _indexCount += VertexBuffer.NumberOfVerticesOfLine;
            }
        }

        public struct RawAccessor : IDisposable
        {
            private readonly VertexBuffer _vb;
            public readonly NativeArray<uint> Indices;
            public readonly NativeArray<Vertex> Vertices;

            internal RawAccessor(VertexBuffer vb)
            {
                _vb = vb;
                _vb.Clear();
                Indices = vb.Indices;
                Vertices = vb.Vertices;
            }

            public void Dispose()
            {
                _vb.EndRawAccess(ref this);
            }

            public void AddSubMesh(int indexStart, int indexCount)
            {
                var s = indexStart * NumberOfVerticesOfLine;
                var c = indexCount * NumberOfVerticesOfLine;
                _vb._subMeshes.Add(new ReadonlySubMesh(s, c));
            }
        }

        public const int NumberOfVerticesOfLine = 6;
        private int _numLines = 0;
        private List<ReadonlySubMesh> _subMeshes = new List<ReadonlySubMesh>();
        public int LineCapacity => _numLines;
        public int LineCount => _vertexCount / NumberOfVerticesOfLine;
        public int VertexCapacity => _numLines * NumberOfVerticesOfLine;
        public int AutoExpandScale = 0;
        private int _vertexCount;
        private bool _isInCreateSubMesh;
        private NativeArray<uint> Indices;
        private NativeArray<Vertex> Vertices;
        private Mesh _targetMesh;

        public VertexBuffer(int nline, Mesh targetMesh = null)
        {
            SetLineCapacity(nline);
            _targetMesh = targetMesh;
        }

        public void Dispose()
        {
            if (Indices.IsCreated) Indices.Dispose();
            if (Vertices.IsCreated) Vertices.Dispose();
            _targetMesh = null;
        }

        public void SetLineCapacity(int nline)
        {
            AllocateBuffer(nline);
        }

        public void Clear()
        {
            _subMeshes.Clear();
            _vertexCount = 0;
        }

        private void AllocateBuffer(int nline)
        {
            Assert.IsTrue(_numLines < nline);
            int len = nline * NumberOfVerticesOfLine;
            if (!Indices.IsCreated)
            {
                Indices = new NativeArray<uint>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Vertices = new NativeArray<Vertex>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Clear();
            }
            else
            {
                var newIndices = new NativeArray<uint>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                var newBuffer = new NativeArray<Vertex>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeArray<uint>.Copy(Indices, 0, newIndices, 0, Indices.Length);
                NativeArray<Vertex>.Copy(Vertices, 0, newBuffer, 0, Vertices.Length);
                Indices.Dispose();
                Vertices.Dispose();
                Indices = newIndices;
                Vertices = newBuffer;
            }
            _numLines = nline;
        }

        public SubMeshCreator CreateSubMesh()
        {
            _isInCreateSubMesh = true;
            return new SubMeshCreator(this, _vertexCount);
        }
        internal void CreateSubMeshEnd(int start, int count)
        {
            _subMeshes.Add(new ReadonlySubMesh(start, count));
            _isInCreateSubMesh = false;
        }

        public RawAccessor BeginRawAccess()
        {
            return new RawAccessor(this);
        }
        internal void EndRawAccess(ref RawAccessor accessor)
        {
            Assert.AreNotEqual(_subMeshes.Count, 0);

            var last = _subMeshes[_subMeshes.Count - 1];
            _vertexCount = last.IndexStart + last.IndexCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLine(
            NativeArray<Vertex> buf,
            int top,
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            uint flag
        )
        {
            float w = (float)flag;
            float z = 0.0f;
            float m = -1.5f;

            col0.a = 1.0f;
            col1.a = 1.0f;

            var index = top;
            unsafe
            {
                var p = (Vertex*)buf.GetUnsafePtr();
                // 0
                p[index].BeginPos = bgn;
                p[index].Normal0 = nml0;
                p[index].Normal1 = nml1;
                p[index].Color = col0;
                p[index].EndPos = end;
                p[index].Adjuster = new Vector4(1, m, z, w);
                index++;
                // 1
                p[index].BeginPos = end;
                p[index].Normal0 = nml0;
                p[index].Normal1 = nml1;
                p[index].Color = col0;
                p[index].EndPos = bgn;
                p[index].Adjuster = new Vector4(-1, m, z, w);
                index++;
                // 2
                p[index].BeginPos = end;
                p[index].Normal0 = nml0;
                p[index].Normal1 = nml1;
                p[index].Color = col0;
                p[index].EndPos = bgn;
                p[index].Adjuster = new Vector4(1, m, z, w);
                index++;
                // 3
                p[index].BeginPos = bgn;
                p[index].Normal0 = nml0;
                p[index].Normal1 = nml1;
                p[index].Color = col0;
                p[index].EndPos = end;
                p[index].Adjuster = new Vector4(1, m, z, w);
                index++;
                // 4
                p[index].BeginPos = bgn;
                p[index].Normal0 = nml0;
                p[index].Normal1 = nml1;
                p[index].Color = col0;
                p[index].EndPos = end;
                p[index].Adjuster = new Vector4(-1, m, z, w);
                index++;
                // 5
                p[index].BeginPos = end;
                p[index].Normal0 = nml0;
                p[index].Normal1 = nml1;
                p[index].Color = col0;
                p[index].EndPos = bgn;
                p[index].Adjuster = new Vector4(1, m, z, w);
                index++;
            }
        }
        public void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            bool nosmoothangle, bool nocull, bool nofront
        )
        {
            Assert.IsTrue(_isInCreateSubMesh, "Cannot call AddLine when creating SubMesh");

            if (_vertexCount >= VertexCapacity)
            {
                if (AutoExpandScale <= 1)
                {
                    Debug.LogWarning("VertexBuffer is full");
                    return;
                }
                else
                {
                    SetLineCapacity(_numLines * AutoExpandScale);
                }
            }

            uint flag = 0;
            flag |= (nocull)? (uint)LineFlag.NoCull : 0;
            flag |= (nosmoothangle)? (uint)LineFlag.NoSmoothAngle : 0;
            flag |= (nofront)? (uint)LineFlag.NoFront : 0;

            SetLine(Vertices, _vertexCount, bgn, end, nml0, nml1, col0, col1, flag);

            unsafe
            {
                var indices = (uint*)Indices.GetUnsafePtr();
                // 0
                indices[_vertexCount] = (uint)_vertexCount++;
                // 1
                indices[_vertexCount] = (uint)_vertexCount++;
                // 2
                indices[_vertexCount] = (uint)_vertexCount++;
                // 3
                indices[_vertexCount] = (uint)_vertexCount++;
                // 4
                indices[_vertexCount] = (uint)_vertexCount++;
                // 5
                indices[_vertexCount] = (uint)_vertexCount++;
            }
        }

        private void AttachToMesh(Mesh mesh)
        {
            mesh.SetVertexBufferParams(_vertexCount, Layout);
            mesh.SetVertexBufferData(Vertices, 0, 0, _vertexCount, 0, MeshUpdateFlags.Default);

            mesh.SetIndexBufferParams(_vertexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(Indices, 0, 0, _vertexCount, MeshUpdateFlags.Default);

            mesh.subMeshCount = _subMeshes.Count;
            for (int i = 0; i < _subMeshes.Count; ++i)
            {
                var sm = _subMeshes[i];
                var desc = new SubMeshDescriptor(sm.IndexStart, sm.IndexCount, MeshTopology.Triangles);
                mesh.SetSubMesh(i, desc, MeshUpdateFlags.Default);
            }
        }

        public void ApplyToMesh(Mesh mesh)
        {
            mesh.Clear();
            AttachToMesh(mesh);
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
