using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Yoji;

namespace Yoji.Editor
{
    public class FrameConstructor
    {
        public class Wire
        {
            public int SubMeshIndex { get; }
            public Position BeginPosition { get; }
            public Position EndPosition { get; }
            public Vector3 LeftPosition { get; }
            public Vector3 RightPosition { get; }
            public Normal NormalA { get; }
            public Normal NormalB { get; private set; }
            public Color BeginColor { get; }
            public Color EndColor { get; }
            public BoneWeight BeginBoneWeight { get; internal set; }
            public BoneWeight EndBoneWeight { get; internal set; }
            public int Priority { get; }
            public bool NoSmooth { get; set; }
            public bool NoCull { get; set; }
            public bool NoFront { get; set; }
            //
            public Wire(int subMeshIndex, Position vtxA, Position vtxB, Vector3 left, Vector3 right, Normal nmlA, Normal nmlB, Color colA, Color colB, int priority)
            {
                SubMeshIndex = subMeshIndex;
                BeginPosition = vtxA;
                EndPosition = vtxB;
                LeftPosition = left;
                RightPosition = right;
                NormalA = nmlA;
                NormalB = nmlB;
                BeginColor = colA;
                EndColor = colB;
                Priority = priority;
            }

            public void AdjustNormal()
            {
                NormalB = NormalA;
            }
        }

        private readonly TriangleProvider _provider;
        private List<Wire> _wires = new List<Wire>();
        private Dictionary<TriangleEdgeId, int> _wireMap = new Dictionary<TriangleEdgeId, int>();
        public bool DestroyUselessWire = true;

        public FrameConstructor(TriangleProvider provider)
        {
            _provider = provider;
        }

        public void Construct()
        {
            foreach (var triangle in _provider.AllTriangles)
            {
                AddTriangle(triangle, 0);
            }
        }

        private int AddTriangle(Triangle triangle, int priority)
        {
            if (triangle.IsIndependent)
            {
                var longestEdge = triangle.FindLongestEdge();
                return AddWire(triangle, longestEdge, priority, true);
            }
            int count = 0;
            foreach (var edge in TriangleEdge.Values)
            {
                count += AddWire(triangle, edge, priority, false);
            }
            return count;
        }

        private int AddWire(Triangle triangle, TriangleEdge edge, int priority, bool nocull)
        {
            var id = triangle.GetEdgeID(edge);
            var smi = triangle.SubMeshIndex;
            var positions = triangle.GetPositions(edge);

            var left = triangle.GetPosition(edge.Next.First);

            var selfNormal = (nocull)? Normal.Invalid : triangle.FaceNormal;
            var other = _provider.FindAdjacentTriangle(triangle, edge);
            if (!nocull && other == null) return 0;
            //var right = other
            var otherNormal = (other != null)? other.FaceNormal : Normal.Invalid;
            var colors = triangle.GetColors(edge);

            var selfSubMesh = triangle.SubMeshIndex;
            var otherSubMesh = (other == null)? -1 : other.SubMeshIndex;
            var otherIsFin = (other == null)? false : other.IsFin;

            if (
                !nocull &&
                DestroyUselessWire &&
                selfNormal == otherNormal &&
                selfSubMesh == otherSubMesh &&
                !otherIsFin
            ) return 0;

            var vtxA = positions.First;
            var vtxB = positions.Second;
            var nmlA = selfNormal;
            var nmlB = otherNormal;
            var wire = new Wire(smi, vtxA, vtxB, (Vector3)left, Vector3.one, nmlA, nmlB, colors.First, colors.Second, priority);
            if (triangle.HasBoneWeights)
            {
                wire.BeginBoneWeight = triangle.GetBoneWeight(edge.First);
                wire.EndBoneWeight = triangle.GetBoneWeight(edge.Second);
            }
            wire.NoCull = nocull;
            wire.NoSmooth = wire.NoFront = otherIsFin;
            //Debug.Log($"normal: {nmlA} {nmlB}");
            return AddWire(id, wire, priority, triangle.IsFin);
        }

        private int AddWire(TriangleEdgeId id, Wire wire, int priority, bool isFin)
        {
            if (!_wireMap.ContainsKey(id))
            {
                _wireMap[id] = _wires.Count;
                _wires.Add(wire);
                return 1;
            }
            //
            var existsWireIndex = _wireMap[id];
            var existsWire = _wires[existsWireIndex];
            if (existsWire.SubMeshIndex != wire.SubMeshIndex)// || isFin)
            {
                existsWire.NoSmooth = wire.NoSmooth = true;
                existsWire.NoFront = wire.NoFront = true;
                if (existsWire.Priority < priority)
                {
                    _wires[existsWireIndex] = wire;
                }
                Debug.Log($"same {_wires[existsWireIndex].NormalA} {_wires[existsWireIndex].NormalB}");
                //_wires[existsWireIndex].AdjustNormal();
            }
            return 0;
        }

        public int SubMeshCount { get => (_wires.Count == 0)? 1 : _wires.Max((w) => w.SubMeshIndex) + 1; }

        public int CountWireBySubMesh(int submeshindex)
        {
            return _wires.Count((w) => w.SubMeshIndex == submeshindex);
        }

        public VertexBuffer ToVertexBuffer()
        {
            int nline = _wires.Count;
            var vb = new VertexBuffer(nline);
            for (int smi = 0; smi < SubMeshCount; ++smi)
            {
                using (var sm = vb.CreateSubMesh())
                {
                    for (var i = 0; i < nline; ++i)
                    {
                        var wire = _wires[i];
                        if (wire.SubMeshIndex != smi) continue;
                        sm.AddLine(
                            (Vector3)wire.BeginPosition,
                            (Vector3)wire.EndPosition,
                            (Vector3)wire.NormalA,
                            (Vector3)wire.NormalB,
                            wire.BeginColor,
                            wire.EndColor,
                            wire.NoSmooth,
                            wire.NoCull,
                            wire.NoFront
                        );
                    }
                }
            }
            return vb;
        }

        public FrameStructure ToFrameStructure()
        {
            int nline = _wires.Count;
            var fs = ScriptableObject.CreateInstance<FrameStructure>();
            using (var editor = fs.BeginEdit())
            {
                for (int smi = 0; smi < SubMeshCount; ++smi)
                {
                    using (var subMesh = editor.BeginCreateSubMesh())
                    {
                        for (var i = 0; i < nline; ++i)
                        {
                            var wire = _wires[i];
                            if (wire.SubMeshIndex != smi) continue;
                            subMesh.AddLine(
                                (Vector3)wire.BeginPosition,
                                (Vector3)wire.EndPosition,
                                (Vector3)wire.NormalA,
                                (Vector3)wire.NormalB,
                                wire.BeginColor,
                                wire.EndColor,
                                wire.BeginBoneWeight,
                                wire.EndBoneWeight,
                                wire.NoSmooth,
                                wire.NoCull,
                                wire.NoFront
                            );
                        }
                    }
                }
            }
            return fs;
        }
    }
}

