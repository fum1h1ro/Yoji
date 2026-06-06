using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Yoji.Runtime.Components
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

            SetupFrameStructure();
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
                SetupFrameStructure();
                _oldFrameStructure = FrameStructure;
            }
            MakeMesh();
            _meshRenderer.sharedMaterials = Materials;
        }

        private void SetupFrameStructure()
        {
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
            [NativeDisableParallelForRestriction]
            public NativeArray<uint> Indices;
            [NativeDisableParallelForRestriction]
            public NativeArray<VertexBuffer.Vertex> Vertices;

            public void Execute(int index)
            {
                var vbtop = index * VertexBuffer.NumberOfVerticesOfLine;
                var line = Lines[index];

                float3 beginPos = line.BeginPos;
                float3 endPos = line.EndPos;
                float3 normal0 = line.Normal0;
                float3 normal1 = line.Normal1;

                float4x4 beginMtx0 = Bones[(int)line.BeginWeight.Indices[0]];
                beginPos = math.transform(beginMtx0, beginPos);

                // 正確な法線変換
                float3x3 m3 = (float3x3)beginMtx0; // 上位3x3
                float3x3 invTranspose = math.transpose(math.inverse(m3));
                normal0 = math.normalize(math.mul(invTranspose, normal0));
                normal1 = math.normalize(math.mul(invTranspose, normal1));

                float4x4 endMtx0 = Bones[(int)line.EndWeight.Indices[0]];
                endPos = math.transform(endMtx0, endPos);

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
