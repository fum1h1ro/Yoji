using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using EdgeID = Yoji.Editor.TriangleProvidor.EdgeID;
using Edge = Yoji.Editor.TriangleProvidor.Edge;
using Triangle = Yoji.Editor.TriangleProvidor.Triangle;
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

            public void Reverse()
            {
                Vector *= -1;
            }
        }

        public class Wire
        {
            public int SubMeshIndex { get; }
            public Vertex VertexA { get; }
            public Vertex VertexB { get; }
            public Normal NormalA { get; }
            public Normal NormalB { get; private set; }
            public Color ColorA { get; }
            public Color ColorB { get; }
            public int Priority { get; }
            public bool NoSmooth { get; set; }
            public bool NoCull { get; set; }
            public bool NoFront { get; set; }
            //
            public Wire(int subMeshIndex, Vertex vtxA, Vertex vtxB, Normal nmlA, Normal nmlB, Color colA, Color colB, int priority)
            {
                SubMeshIndex = subMeshIndex;
                VertexA = vtxA;
                VertexB = vtxB;
                NormalA = nmlA;
                NormalB = nmlB;
                ColorA = colA;
                ColorB = colB;
                Priority = priority;
            }

            public void AdjustNormal()
            {
                NormalB = NormalA;
            }
        }

        private List<Wire> Wires = new List<Wire>();
        private Dictionary<EdgeID, int> WireMap = new Dictionary<EdgeID, int>();
        public bool DestroyUselessWire = true;

        public FrameConstructor()
        {
        }

        public int AddTriangle(Triangle triangle, int priority)
        {
            if (triangle.IsIndependent)
            {
                var longestEdge = triangle.FindLongestEdge();
                Debug.Log("ADD");
                return AddWire(triangle, longestEdge, priority, true);
            }
            int count = 0;
            foreach (var edge in TriangleProvidor.Edge.Values)
            {
                count += AddWire(triangle, edge, priority, false);
            }
            return count;
        }

        public int AddWire(Triangle triangle, Edge edge, int priority, bool nocull)
        {
            var id = triangle.GetEdgeID(edge);
            var smi = triangle.SubMeshIndex;
            var positions = triangle.GetPositions(edge);
            var selfNormal = (nocull)? Vector3.zero : triangle.FaceNormal;
            var other = triangle.GetOtherTriangle(edge);
            if (!nocull && other == null) return 0;
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
            var wire = new Wire(smi, vtxA, vtxB, nmlA, nmlB, colors.First, colors.Second, priority);
            wire.NoCull = nocull;
            wire.NoSmooth = wire.NoFront = otherIsFin;
            //Debug.Log($"normal: {nmlA.Vector} {nmlB.Vector}");
            return AddWire(id, wire, priority, triangle.IsFin);
        }

        private int AddWire(EdgeID id, Wire wire, int priority, bool isFin)
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
                    foreach (var wire in Wires.Where(x => x.SubMeshIndex == smi))
                    {
                        sm.AddLine(
                            wire.VertexA.Position,
                            wire.VertexB.Position,
                            wire.NormalA.Vector,
                            wire.NormalB.Vector,
                            wire.ColorA,
                            wire.ColorB,
                            wire.NoSmooth,
                            wire.NoCull,
                            wire.NoFront);
                    }
                }
            }
            return vb;
        }
    }
}

