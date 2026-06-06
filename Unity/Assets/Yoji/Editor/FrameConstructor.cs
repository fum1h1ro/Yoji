using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Yoji.Runtime;

namespace Yoji.Editor
{
    public class FrameConstructor
    {
        public class Vertex : IEquatable<Vertex>
        {
            public Vector3 Position { get; }

            internal Vertex(Vector3 pos)
            {
                Position = pos;
            }

            public bool Equals(Vertex b)
            {
                return Position == b.Position;
            }
        }

        public class Normal
        {
            public Vector3 Vector { get; private set; }

            internal Normal(Vector3 vec)
            {
                Vector = vec;
            }
        }

        public class Wire
        {
            public int SubMeshIndex { get; }
            public Vertex BeginPosition { get; }
            public Vertex EndPosition { get; }
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
            public Wire(int subMeshIndex, Vertex vtxA, Vertex vtxB, Vector3 left, Vector3 right, Normal nmlA, Normal nmlB, Color colA, Color colB, int priority)
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
        private List<Wire> Wires = new List<Wire>();
        private Dictionary<TriangleEdgeId, int> WireMap = new Dictionary<TriangleEdgeId, int>();
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

            var selfNormal = (nocull)? Vector3.zero : triangle.FaceNormal;
            var other = _provider.FindAdjacentTriangle(triangle, edge);
            if (!nocull && other == null) return 0;
            //var right = other
            var otherNormal = (other != null)? other.FaceNormal : Vector3.zero;
            var colors = triangle.GetColors(edge);

            var selfSubMesh = triangle.SubMeshIndex;
            var otherSubMesh = (other == null)? -1 : other.SubMeshIndex;
            var otherIsFin = (other == null)? false : other.IsFin;

            if (!nocull && DestroyUselessWire && selfNormal == otherNormal && selfSubMesh == otherSubMesh && !otherIsFin) return 0;

            var vtxA = new Vertex(positions.First);
            var vtxB = new Vertex(positions.Second);
            var nmlA = new Normal(selfNormal);
            var nmlB = new Normal(otherNormal);
            var wire = new Wire(smi, vtxA, vtxB, left, Vector3.one, nmlA, nmlB, colors.First, colors.Second, priority);
            if (triangle.HasBoneWeights)
            {
                wire.BeginBoneWeight = triangle.GetBoneWeight(edge.First);
                wire.EndBoneWeight = triangle.GetBoneWeight(edge.Second);
            }
            wire.NoCull = nocull;
            wire.NoSmooth = wire.NoFront = otherIsFin;
            //Debug.Log($"normal: {nmlA.Vector} {nmlB.Vector}");
            return AddWire(id, wire, priority, triangle.IsFin);
        }

        private int AddWire(TriangleEdgeId id, Wire wire, int priority, bool isFin)
        {
            if (!WireMap.ContainsKey(id))
            {
                WireMap[id] = Wires.Count;
                Wires.Add(wire);
                return 1;
            }
            //
            var existsWireIndex = WireMap[id];
            var existsWire = Wires[existsWireIndex];
            if (existsWire.SubMeshIndex != wire.SubMeshIndex)// || isFin)
            {
                existsWire.NoSmooth = wire.NoSmooth = true;
                existsWire.NoFront = wire.NoFront = true;
                if (existsWire.Priority < priority)
                {
                    Wires[existsWireIndex] = wire;
                }
                Debug.Log($"same {Wires[existsWireIndex].NormalA.Vector} {Wires[existsWireIndex].NormalB.Vector}");
                //Wires[existsWireIndex].AdjustNormal();
            }
            return 0;
        }

        public int SubMeshCount { get => (Wires.Count == 0)? 1 : Wires.Max((w) => w.SubMeshIndex) + 1; }

        public int CountWireBySubMesh(int submeshindex)
        {
            return Wires.Count((w) => w.SubMeshIndex == submeshindex);
        }

        public VertexBuffer ToVertexBuffer()
        {
            int nline = Wires.Count;
            var vb = new VertexBuffer(nline);
            for (int smi = 0; smi < SubMeshCount; ++smi)
            {
                using (var sm = vb.CreateSubMesh())
                {
                    for (var i = 0; i < nline; ++i)
                    {
                        var wire = Wires[i];
                        if (wire.SubMeshIndex != smi) continue;
                        sm.AddLine(
                            wire.BeginPosition.Position,
                            wire.EndPosition.Position,
                            wire.NormalA.Vector,
                            wire.NormalB.Vector,
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
            int nline = Wires.Count;
            var fs = ScriptableObject.CreateInstance<FrameStructure>();
            using (var editor = fs.BeginEdit())
            {
                for (int smi = 0; smi < SubMeshCount; ++smi)
                {
                    using (var subMesh = editor.BeginCreateSubMesh())
                    {
                        for (var i = 0; i < nline; ++i)
                        {
                            var wire = Wires[i];
                            if (wire.SubMeshIndex != smi) continue;
                            subMesh.AddLine(
                                wire.BeginPosition.Position,
                                wire.EndPosition.Position,
                                wire.NormalA.Vector,
                                wire.NormalB.Vector,
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

