using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Yoji.Runtime
{
    public interface ISubMeshCreator : IDisposable
    {
        void AddLine(Vector3 bgn, Vector3 end, Color col0, Color col1);
        void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1);
        void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, bool nosmoothangle, bool nocull, bool nofront);
        void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, BoneWeight wgt0, BoneWeight wgt1, bool nosmoothangle, bool nocull, bool nofront);
    }

    public interface IVertexBuffer : IDisposable
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VertexEmpty
        {
        }

        int LineCapacity { get; }
        int LineCount { get; }
        void Clear();
        void SetLineCapacity(int nline);
        ISubMeshCreator CreateSubMesh();
        void EndCreate(int start, int count);
        void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            bool nosmoothangle, bool nocull, bool nofront
        );
        void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            BoneWeight wgt0, BoneWeight wgt1,
            bool nosmoothangle, bool nocull, bool nofront
        );
        void ApplyToMesh(Mesh mesh);
        Mesh ToMesh(string name);

        VertexAttributeDescriptor[] GetLayout();
    }

    public abstract class VertexBufferImpl<T0, T1, T2, T3> : IVertexBuffer
        where T0 : struct
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        [System.Flags]
        protected enum Flag
        {
            NoCull = (1<<0),
            NoSmoothAngle = (1<<1),
            NoFront = (1<<2),
        }

        public struct SubMeshCreator : IDisposable, ISubMeshCreator
        {
            private IVertexBuffer VB;
            private int IndexStart;
            private int IndexCount;

            internal SubMeshCreator(IVertexBuffer vb, int baseIndex)
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
                IndexCount += VertexBufferImpl<T0, T1, T2, T3>.NumberOfVerticesOfLine;
            }
            public void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, BoneWeight wgt0, BoneWeight wgt1, bool nosmoothangle, bool nocull, bool nofront)
            {
                VB.AddLine(bgn, end, nml0, nml1, col0, col1, wgt0, wgt1, nosmoothangle, nocull, nofront);
                IndexCount += VertexBufferImpl<T0, T1, T2, T3>.NumberOfVerticesOfLine;
            }
        }

        private const int NumberOfVerticesOfLine = 6;
        protected int NumLines = 0;
        private List<(int, int)> SubMesh = new List<(int, int)>();
        public int LineCapacity => NumLines;
        public int LineCount => VertexCount / NumberOfVerticesOfLine;
        protected int VertexCapacity => NumLines * NumberOfVerticesOfLine;
        public int AutoExpandScale = 0;
        protected int VertexCount;
        protected bool IsCreateSubMesh { get; private set; }
        protected NativeArray<uint> Indices;
        protected NativeArray<T0> Buffer0;
        protected NativeArray<T1> Buffer1;
        protected NativeArray<T2> Buffer2;
        protected NativeArray<T3> Buffer3;

        private bool HasStream0 => true;
        private bool HasStream1 => typeof(T1) != typeof(IVertexBuffer.VertexEmpty);
        private bool HasStream2 => typeof(T2) != typeof(IVertexBuffer.VertexEmpty);
        private bool HasStream3 => typeof(T3) != typeof(IVertexBuffer.VertexEmpty);

        public VertexBufferImpl(int nline)
        {
            SetLineCapacity(nline);
        }

        public void Dispose()
        {
            if (Indices.IsCreated) Indices.Dispose();
            if (Buffer0.IsCreated) Buffer0.Dispose();
            if (Buffer1.IsCreated) Buffer1.Dispose();
            if (Buffer2.IsCreated) Buffer2.Dispose();
            if (Buffer3.IsCreated) Buffer3.Dispose();
        }

        public abstract VertexAttributeDescriptor[] GetLayout();

        public void SetLineCapacity(int nline)
        {
            AllocateBuffer(nline);
        }

        public void Clear()
        {
            SubMesh.Clear();
            VertexCount = 0;
        }

        private void AllocateBuffer(int nline)
        {
            Assert.IsTrue(NumLines < nline);
            int len = nline * NumberOfVerticesOfLine;
            if (!Indices.IsCreated)
            {
                Indices = new NativeArray<uint>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Buffer0 = new NativeArray<T0>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                if (HasStream1) Buffer1 = new NativeArray<T1>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                if (HasStream2) Buffer2 = new NativeArray<T2>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                if (HasStream3) Buffer3 = new NativeArray<T3>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Clear();
            }
            else
            {
                var newIndices = new NativeArray<uint>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                var newBuffer0 = new NativeArray<T0>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeArray<uint>.Copy(Indices, 0, newIndices, 0, Indices.Length);
                NativeArray<T0>.Copy(Buffer0, 0, newBuffer0, 0, Buffer0.Length);
                Indices.Dispose();
                Buffer0.Dispose();
                Indices = newIndices;
                Buffer0 = newBuffer0;
                if (HasStream1)
                {
                    var newBuffer1 = new NativeArray<T1>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    NativeArray<T1>.Copy(Buffer1, 0, newBuffer1, 0, Buffer1.Length);
                    Buffer1.Dispose();
                    Buffer1 = newBuffer1;
                }
                if (HasStream2)
                {
                    var newBuffer2 = new NativeArray<T2>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    NativeArray<T2>.Copy(Buffer2, 0, newBuffer2, 0, Buffer2.Length);
                    Buffer2.Dispose();
                    Buffer2 = newBuffer2;
                }
                if (HasStream3)                {
                    var newBuffer3 = new NativeArray<T3>(len, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    NativeArray<T3>.Copy(Buffer3, 0, newBuffer3, 0, Buffer3.Length);
                    Buffer3.Dispose();
                    Buffer3 = newBuffer3;
                }
            }
            NumLines = nline;
        }

        public ISubMeshCreator CreateSubMesh()
        {
            IsCreateSubMesh = true;
            return new SubMeshCreator(this, VertexCount);
        }
        public void EndCreate(int start, int count)
        {
            SubMesh.Add((start, count));
            IsCreateSubMesh = false;
        }

        public virtual void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            bool nosmoothangle, bool nocull, bool nofront
        )
        {
        }
        public virtual void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            BoneWeight wgt0, BoneWeight wgt1,
            bool nosmoothangle, bool nocull, bool nofront
        )
        {
        }

        public void ApplyToMesh(Mesh mesh)
        {
            mesh.Clear();

            mesh.SetVertexBufferParams(VertexCount, GetLayout());
            mesh.SetVertexBufferData(Buffer0, 0, 0, VertexCount, 0, MeshUpdateFlags.Default);
            if (HasStream1) mesh.SetVertexBufferData(Buffer1, 0, 0, VertexCount, 1, MeshUpdateFlags.Default);
            if (HasStream2) mesh.SetVertexBufferData(Buffer2, 0, 0, VertexCount, 2, MeshUpdateFlags.Default);
            if (HasStream3) mesh.SetVertexBufferData(Buffer3, 0, 0, VertexCount, 3, MeshUpdateFlags.Default);

            mesh.SetIndexBufferParams(VertexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(Indices, 0, 0, VertexCount, MeshUpdateFlags.Default);

            mesh.subMeshCount = SubMesh.Count;
            Debug.Log($"ApplyToMesh: VertexCount={VertexCount}, SubMeshCount={SubMesh.Count}");
            for (int i = 0; i < SubMesh.Count; ++i)
            {
                var sm = SubMesh[i];
                var desc = new SubMeshDescriptor(0, 0, MeshTopology.Triangles);
                desc.indexStart = sm.Item1;
                desc.indexCount = sm.Item2;
                mesh.SetSubMesh(i, desc, MeshUpdateFlags.Default);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh();
            mesh.name = name;
            ApplyToMesh(mesh);
            return mesh;
        }
    }

    public sealed class VertexBuffer
        : VertexBufferImpl<
            VertexBuffer.Vertex,
            IVertexBuffer.VertexEmpty,
            IVertexBuffer.VertexEmpty,
            IVertexBuffer.VertexEmpty
          >
    {

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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

        public VertexBuffer(int nline) : base(nline)
        {
        }

        public override VertexAttributeDescriptor[] GetLayout()
        {
            return Layout;
        }

        public override void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            bool nosmoothangle, bool nocull, bool nofront
        )
        {
            Assert.IsTrue(IsCreateSubMesh, "Cannot call AddLine when creating SubMesh");

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

            col0.a = 1.0f;
            col1.a = 1.0f;

            unsafe {
                var buf0 = (Vertex*)Buffer0.GetUnsafePtr();

                // 0
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = bgn;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf0[VertexCount].Color = col0;
                buf0[VertexCount].EndPos = end;
                buf0[VertexCount].Adjuster = new Vector4(1, m, z, w);
                VertexCount++;
                // 1
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = end;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf0[VertexCount].Color = col0;
                buf0[VertexCount].EndPos = bgn;
                buf0[VertexCount].Adjuster = new Vector4(-1, m, z, w);
                VertexCount++;
                // 2
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = end;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf0[VertexCount].Color = col0;
                buf0[VertexCount].EndPos = bgn;
                buf0[VertexCount].Adjuster = new Vector4(1, m, z, w);
                VertexCount++;
                // 3
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = bgn;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf0[VertexCount].Color = col0;
                buf0[VertexCount].EndPos = end;
                buf0[VertexCount].Adjuster = new Vector4(1, m, z, w);
                VertexCount++;
                // 4
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = bgn;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf0[VertexCount].Color = col0;
                buf0[VertexCount].EndPos = end;
                buf0[VertexCount].Adjuster = new Vector4(-1, m, z, w);
                VertexCount++;
                // 5
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = end;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf0[VertexCount].Color = col0;
                buf0[VertexCount].EndPos = bgn;
                buf0[VertexCount].Adjuster = new Vector4(1, m, z, w);
                VertexCount++;
            }
        }
    }

    public sealed class VertexBufferSkinned
        : VertexBufferImpl<
            VertexBufferSkinned.Vertex,
            VertexBufferSkinned.Vertex1,
            VertexBufferSkinned.Vertex2,
            IVertexBuffer.VertexEmpty
          >
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Vertex
        {
            public Vector3 BeginPos;
            public Vector3 Normal0;
            public Vector4 Normal1;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Vertex1
        {
            public Color32 Color;
            public Vector3 EndPos;
            public Vector4 Adjuster;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Vertex2
        {
            public Vector4 BlendWeight;
            public ushort BlendIndex0, BlendIndex1, BlendIndex2, BlendIndex3;
        }
        private readonly static VertexAttributeDescriptor[] Layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UInt8, 4, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 1),
            new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.BlendIndices, VertexAttributeFormat.UInt16, 4, 2),
        };

        public VertexBufferSkinned(int nline) : base(nline)
        {
        }

        public override VertexAttributeDescriptor[] GetLayout()
        {
            return Layout;
        }

        public override void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            BoneWeight wgt0, BoneWeight wgt1,
            bool nosmoothangle, bool nocull, bool nofront
        )
        {
            Assert.IsTrue(IsCreateSubMesh, "Cannot call AddLine when creating SubMesh");

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

            col0.a = 1.0f;
            col1.a = 1.0f;

            unsafe {
                var buf0 = (Vertex*)Buffer0.GetUnsafePtr();
                var buf1 = (Vertex1*)Buffer1.GetUnsafePtr();
                var buf2 = (Vertex2*)Buffer2.GetUnsafePtr();

                // 0
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = bgn;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf1[VertexCount].Color = col0;
                buf1[VertexCount].EndPos = end;
                buf1[VertexCount].Adjuster = new Vector4(1, m, z, w);
                buf2[VertexCount].BlendWeight = new Vector4(wgt0.weight0, wgt0.weight1, wgt0.weight2, wgt0.weight3);
                buf2[VertexCount].BlendIndex0 = (ushort)wgt0.boneIndex0;
                buf2[VertexCount].BlendIndex1 = (ushort)wgt0.boneIndex1;
                buf2[VertexCount].BlendIndex2 = (ushort)wgt0.boneIndex2;
                buf2[VertexCount].BlendIndex3 = (ushort)wgt0.boneIndex3;
                VertexCount++;
                // 1
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = end;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf1[VertexCount].Color = col0;
                buf1[VertexCount].EndPos = bgn;
                buf1[VertexCount].Adjuster = new Vector4(-1, m, z, w);
                buf2[VertexCount].BlendWeight = new Vector4(wgt1.weight0, wgt1.weight1, wgt1.weight2, wgt1.weight3);
                buf2[VertexCount].BlendIndex0 = (ushort)wgt1.boneIndex0;
                buf2[VertexCount].BlendIndex1 = (ushort)wgt1.boneIndex1;
                buf2[VertexCount].BlendIndex2 = (ushort)wgt1.boneIndex2;
                buf2[VertexCount].BlendIndex3 = (ushort)wgt1.boneIndex3;
                VertexCount++;
                // 2
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = end;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf1[VertexCount].Color = col0;
                buf1[VertexCount].EndPos = bgn;
                buf1[VertexCount].Adjuster = new Vector4(1, m, z, w);
                buf2[VertexCount].BlendWeight = new Vector4(wgt1.weight0, wgt1.weight1, wgt1.weight2, wgt1.weight3);
                buf2[VertexCount].BlendIndex0 = (ushort)wgt1.boneIndex0;
                buf2[VertexCount].BlendIndex1 = (ushort)wgt1.boneIndex1;
                buf2[VertexCount].BlendIndex2 = (ushort)wgt1.boneIndex2;
                buf2[VertexCount].BlendIndex3 = (ushort)wgt1.boneIndex3;
                VertexCount++;
                // 3
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = bgn;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf1[VertexCount].Color = col0;
                buf1[VertexCount].EndPos = end;
                buf1[VertexCount].Adjuster = new Vector4(1, m, z, w);
                buf2[VertexCount].BlendWeight = new Vector4(wgt0.weight0, wgt0.weight1, wgt0.weight2, wgt0.weight3);
                buf2[VertexCount].BlendIndex0 = (ushort)wgt0.boneIndex0;
                buf2[VertexCount].BlendIndex1 = (ushort)wgt0.boneIndex1;
                buf2[VertexCount].BlendIndex2 = (ushort)wgt0.boneIndex2;
                buf2[VertexCount].BlendIndex3 = (ushort)wgt0.boneIndex3;
                VertexCount++;
                // 4
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = bgn;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf1[VertexCount].Color = col0;
                buf1[VertexCount].EndPos = end;
                buf1[VertexCount].Adjuster = new Vector4(-1, m, z, w);
                buf2[VertexCount].BlendWeight = new Vector4(wgt0.weight0, wgt0.weight1, wgt0.weight2, wgt0.weight3);
                buf2[VertexCount].BlendIndex0 = (ushort)wgt0.boneIndex0;
                buf2[VertexCount].BlendIndex1 = (ushort)wgt0.boneIndex1;
                buf2[VertexCount].BlendIndex2 = (ushort)wgt0.boneIndex2;
                buf2[VertexCount].BlendIndex3 = (ushort)wgt0.boneIndex3;
                VertexCount++;
                // 5
                Indices[VertexCount] = (uint)VertexCount;
                buf0[VertexCount].BeginPos = end;
                buf0[VertexCount].Normal0 = nml0;
                buf0[VertexCount].Normal1 = nml1;
                buf1[VertexCount].Color = col0;
                buf1[VertexCount].EndPos = bgn;
                buf1[VertexCount].Adjuster = new Vector4(1, m, z, w);
                buf2[VertexCount].BlendWeight = new Vector4(wgt1.weight0, wgt1.weight1, wgt1.weight2, wgt1.weight3);
                buf2[VertexCount].BlendIndex0 = (ushort)wgt1.boneIndex0;
                buf2[VertexCount].BlendIndex1 = (ushort)wgt1.boneIndex1;
                buf2[VertexCount].BlendIndex2 = (ushort)wgt1.boneIndex2;
                buf2[VertexCount].BlendIndex3 = (ushort)wgt1.boneIndex3;
                VertexCount++;
            }
        }
    }
}
