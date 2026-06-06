using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Mathematics;


namespace Yoji.Runtime
{
    public class FrameStructure : ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct WeightsAndIndices
        {
            public float4 Weights;
            public uint4 Indices;

            public WeightsAndIndices(float4 weights, uint index0, uint index1, uint index2, uint index3)
            {
                Weights = weights;
                Indices.x = index0;
                Indices.y = index1;
                Indices.z = index2;
                Indices.w = index3;
            }

            public WeightsAndIndices(BoneWeight bw)
            {
                Weights = new float4(bw.weight0, bw.weight1, bw.weight2, bw.weight3);
                Indices = new uint4((uint)bw.boneIndex0, (uint)bw.boneIndex1, (uint)bw.boneIndex2, (uint)bw.boneIndex3);
            }
        }
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct Line
        {
            public Vector3 BeginPos;
            public Vector3 EndPos;
            public Vector3 Normal0;
            public Vector3 Normal1;
            public Color32 BeginColor;
            public Color32 EndColor;
            public WeightsAndIndices BeginWeight;
            public WeightsAndIndices EndWeight;
            public uint Flag;
        }

        public struct Editor : IDisposable
        {
            private FrameStructure _frame;
            private bool _isCreatingSubMesh;

            internal Editor(FrameStructure frame)
            {
                _frame = frame;
                _isCreatingSubMesh = false;
            }

            public void Dispose()
            {
                _frame.EndEdit();
            }

            public SubMeshCreator BeginCreateSubMesh()
            {
                Assert.IsFalse(_isCreatingSubMesh);
                _isCreatingSubMesh = true;
                return new SubMeshCreator(this, _frame._lines.Count);
            }

            internal void EndCreateSubMesh(int indexStart, int indexCount)
            {
                _frame._subMeshes.Add(new SubMesh(indexStart, indexCount));
                _isCreatingSubMesh = false;
            }

            internal void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, BoneWeight wgt0, BoneWeight wgt1, bool nosmoothangle, bool nocull, bool nofront)
            {
                _frame.AddLine(bgn, end, nml0, nml1, col0, col1, wgt0, wgt1, nosmoothangle, nocull, nofront);
            }

            public void AddBindPose(Matrix4x4 bindPose)
            {
                _frame.AddBindPose(bindPose);
            }
        }

        public struct SubMeshCreator : IDisposable
        {
            private Editor _editor;
            private int _start;
            private int _count;

            internal SubMeshCreator(Editor editor, int start)
            {
                _editor = editor;
                _start = start;
                _count = 0;
            }

            public void Dispose()
            {
                _editor.EndCreateSubMesh(_start, _count);
            }

            public void AddLine(Vector3 bgn, Vector3 end, Vector3 nml0, Vector3 nml1, Color col0, Color col1, BoneWeight wgt0, BoneWeight wgt1, bool nosmoothangle, bool nocull, bool nofront)
            {
                _editor.AddLine(bgn, end, nml0, nml1, col0, col1, wgt0, wgt1, nosmoothangle, nocull, nofront);
                _count++;
            }
        }

        public Line[] Lines;
        public SubMesh[] SubMeshes;
        public Matrix4x4[] BindPoses;
        private List<Line> _lines;
        private List<SubMesh> _subMeshes;
        private List<Matrix4x4> _bindPoses;
        private NativeArray<Line> _nativeLines;
        private NativeArray<float4x4> _nativeBindPoses;

        private bool _isEditing = false;

        public int LineCount => Lines?.Length ?? 0;
        public NativeArray<Line> NativeLines => _nativeLines;
        public NativeArray<float4x4> NativeBindPoses => _nativeBindPoses;

        public void OnBeforeSerialize()
        {
            Cleanup();
        }

        public void OnAfterDeserialize()
        {
        }

        private void OnEnable()
        {
            ArrayToNative();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_nativeLines.IsCreated) _nativeLines.Dispose();
            if (_nativeBindPoses.IsCreated) _nativeBindPoses.Dispose();
        }

        public Editor BeginEdit()
        {
            Assert.IsFalse(_isEditing);
            ArrayToList();
            _isEditing = true;
            return new Editor(this);
        }

        private void EndEdit()
        {
            Assert.IsTrue(_isEditing);
            ListToArray();
            ArrayToNative();
            _isEditing = false;
        }

        public void Clear()
        {
            Assert.IsFalse(_isEditing);
            Lines = Array.Empty<Line>();
            if (_nativeLines.IsCreated) _nativeLines.Dispose();
            if (_nativeBindPoses.IsCreated) _nativeBindPoses.Dispose();
            _subMeshes.Clear();
        }

        internal void AddLine(
            Vector3 bgn, Vector3 end,
            Vector3 nml0, Vector3 nml1,
            Color col0, Color col1,
            BoneWeight wgt0, BoneWeight wgt1,
            bool nosmoothangle, bool nocull, bool nofront
        )
        {
            var line = new Line
            {
                BeginPos = bgn,
                EndPos = end,
                Normal0 = nml0,
                Normal1 = nml1,
                BeginColor = col0,
                EndColor = col1,
                BeginWeight = new WeightsAndIndices(wgt0),
                EndWeight = new WeightsAndIndices(wgt1),
                Flag =
                    (nosmoothangle ? (uint)LineFlag.NoSmoothAngle : 0u) |
                    (nocull ? (uint)LineFlag.NoCull : 0u) |
                    (nofront ? (uint)LineFlag.NoFront : 0u)
            };
            _lines.Add(line);
        }

        internal void AddBindPose(Matrix4x4 bindPose)
        {
            _bindPoses.Add(bindPose);
        }

        private void ArrayToList()
        {
            Assert.IsNull(_lines);

            if (Lines == null)
            {
                _lines = new List<Line>();
            }
            else
            {
                _lines = new List<Line>(Lines);
            }

            if (SubMeshes == null)
            {
                _subMeshes = new List<SubMesh>();
            }
            else
            {
                _subMeshes = new List<SubMesh>(SubMeshes);
            }

            if (BindPoses == null)
            {
                _bindPoses = new List<Matrix4x4>();
            }
            else
            {
                _bindPoses = new List<Matrix4x4>(BindPoses);
            }
        }

        private void ListToArray()
        {
            Assert.IsNotNull(_lines);

            Lines = _lines.ToArray();
            SubMeshes = _subMeshes.ToArray();
            BindPoses = _bindPoses.ToArray();
            _lines = null;
            _subMeshes = null;
            _bindPoses = null;
        }

        private void ArrayToNative()
        {
            if (_nativeLines.IsCreated) _nativeLines.Dispose();
            if (_nativeBindPoses.IsCreated) _nativeBindPoses.Dispose();
            _nativeLines = new NativeArray<Line>(Lines, Allocator.Persistent);
            _nativeBindPoses = new NativeArray<float4x4>(BindPoses.Length, Allocator.Persistent);
            for (var i = 0; i < BindPoses.Length; ++i)
            {
                _nativeBindPoses[i] = BindPoses[i];
            }
        }
    }
}
