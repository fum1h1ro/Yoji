using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Yoji.Components
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FrameRenderer : MonoBehaviour
    {
        public FrameStructure FrameStructure;
        public Transform[] Bones;
        public Material[] Materials;

        private Transform _transform;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private FrameStructure _oldFrameStructure;
        private Mesh _mesh;
        private VertexBuffer _vertexBuffer;

#if UNITY_EDITOR
        private void OnBeforeAssemblyReload()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            if (_meshFilter != null) _meshFilter.sharedMesh = null;
            if (_mesh != null)
            {
                DestroyImmediate(_mesh);
                _mesh = null;
            }
            if (_vertexBuffer != null)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = null;
            }
        }
#endif
        private void OnEnable()
        {
            _transform = GetComponent<Transform>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            if (_mesh == null) _mesh = new Mesh();
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;

            if (FrameStructure != null) SetupFrameStructure();
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
        }

        private void OnDisable()
        {
            if (_meshFilter != null) _meshFilter.sharedMesh = null;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_mesh);
                }
                else
                {
                    DestroyImmediate(_mesh);
                }
                _mesh = null;
            }
            if (_vertexBuffer != null)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = null;
            }
        }

        private void LateUpdate()
        {
            if (FrameStructure == null) return;
            if (FrameStructure != _oldFrameStructure)
            {
                if (_oldFrameStructure != null) Destroy(_oldFrameStructure);
                SetupFrameStructure();
                _oldFrameStructure = FrameStructure;
            }
            MakeMesh();
            _meshRenderer.sharedMaterials = Materials;
        }

        private void SetupFrameStructure()
        {
            FrameStructure.PrepareForRuntime();
            var nline = FrameStructure.LineCount;
            if (_vertexBuffer != null) _vertexBuffer.Dispose();
            _vertexBuffer = new VertexBuffer(nline);
        }

        private void MakeMesh()
        {
            var rootInverse = math.inverse(_transform.localToWorldMatrix);
            var bones = new NativeArray<float4x4>(FrameStructure.NativeBindPoses.Length, Allocator.TempJob);
            for (var i = 0; i < Bones.Length; ++i)
            {
                bones[i] = Bones[i].localToWorldMatrix;
            }
            (new CalculateJob()
            {
                RootInverse = rootInverse,
                BindPoses = FrameStructure.NativeBindPoses,
                Bones = bones,
            }).Run(bones.Length);

            using (var rawAcc = _vertexBuffer.BeginRawAccess())
            {
                foreach (var sm in FrameStructure.SubMeshes)
                {
                    rawAcc.AddSubMesh(sm.IndexStart, sm.IndexCount);
                }
                var nline = FrameStructure.LineCount;
                var job = new RenderJob()
                {
                    Bones = bones,
                    Lines = FrameStructure.NativeLines,
                    SourceVertices = FrameStructure.NativeVertices,
                    Indices = rawAcc.Indices,
                    Vertices = rawAcc.Vertices,
                };
                job.Run(nline);

            }
            _vertexBuffer.ApplyToMesh(_mesh);

            bones.Dispose();
        }

        /*
        [BurstCompile]
        private struct ResetDebrisJob : IJobParallelFor
        {
            //[ReadOnly] public PseudoRandom Random;
            [ReadOnly] public NativeArray<float3> Positions;

            public void Execute(int index)
            {
            }
        }

        private void ResetDebris()
        {
            if (!_positions.IsCreated) return;
#if USE_JOB
            var job = new ResetDebrisJob()
            {
                Random = _random,
                Positions = _positions,
                Debris = _debris,
            };

            var handle = job.Schedule(_debris.Length, 0);
            handle.Complete();
#else
            for (var i = 0; i < _debris.Length; ++i)
            {
                var center = _positions[i];
                _debris[i] = MakeDebri(i, center, ref _random);
            }
#endif
        }
        */

        [BurstCompile]
        private struct CalculateJob : IJobParallelFor
        {
            [ReadOnly] public float4x4 RootInverse;
            [ReadOnly] public NativeArray<float4x4> BindPoses;
            public NativeArray<float4x4> Bones;

            public void Execute(int index)
            {
                var m = float4x4.identity;
                m = math.mul(BindPoses[index], m);
                m = math.mul(Bones[index], m);
                m = math.mul(RootInverse, m);
                Bones[index] = m;
            }
        }

        [BurstCompile]
        private struct RenderJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4x4> Bones;
            [ReadOnly] public NativeArray<FrameStructure.Line> Lines;
            [ReadOnly] public NativeArray<FrameStructure.Vertex> SourceVertices;
            [NativeDisableParallelForRestriction]
            public NativeArray<uint> Indices;
            [NativeDisableParallelForRestriction]
            public NativeArray<VertexBuffer.Vertex> Vertices;

            // 4ボーンのLinear Blend Skinning
            static float3 SkinPosition(in FrameStructure.Vertex vertex, in NativeArray<float4x4> bones)
            {
                var w = vertex.Weight.Weights;
                var idx = vertex.Weight.Indices;
                var pos = vertex.Position;
                float3 result = w.x * math.transform(bones[(int)idx.x], pos);
                result += w.y * math.transform(bones[(int)idx.y], pos);
                result += w.z * math.transform(bones[(int)idx.z], pos);
                result += w.w * math.transform(bones[(int)idx.w], pos);
                return result;
            }

            // Triangle.FaceNormal と同じ向きになる面法線
            static float3 CalcFaceNormal(float3 a, float3 b, float3 c)
            {
                return math.normalize(math.cross(math.normalize(a - b), math.normalize(a - c)));
            }

            public void Execute(int index)
            {
                var vbtop = index * VertexBuffer.NumberOfVerticesOfLine;
                var line = Lines[index];

                float3 beginPos = SkinPosition(SourceVertices[line.BeginIndex], Bones);
                float3 endPos = SkinPosition(SourceVertices[line.EndIndex], Bones);
                float3 leftPos = SkinPosition(SourceVertices[line.LeftIndex], Bones);

                float3 normal0 = CalcFaceNormal(beginPos, endPos, leftPos);

                bool noCull = (line.Flag & (uint)LineFlag.NoCull) != 0;
                float3 normal1;
                if (noCull)
                {
                    // シェーダー側はnoCull時にnormal1を参照しない
                    normal1 = normal0;
                }
                else
                {
                    float3 rightPos = SkinPosition(SourceVertices[line.RightIndex], Bones);
                    // 隣接三角形はbegin/endの巻き方向が逆になるため基準点をendにする
                    normal1 = CalcFaceNormal(endPos, beginPos, rightPos);
                }

                VertexBuffer.SetLine(
                    Vertices,
                    vbtop,
                    beginPos,
                    endPos,
                    normal0,
                    normal1,
                    line.BeginColor,
                    line.EndColor,
                    line.Flag
                );

                for (var i = 0; i < VertexBuffer.NumberOfVerticesOfLine; ++i)
                {
                    Indices[vbtop + i] = (uint)(vbtop + i);
                }
            }
        }
    }
}
