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
            public int BeginIndex { get; }
            public int EndIndex { get; }
            public int LeftIndex { get; }
            public int RightIndex { get; }
            public Position BeginPosition { get; }
            public Position EndPosition { get; }
            public Position LeftPosition { get; }
            public Position RightPosition { get; }
            public Normal NormalA { get; }
            public Normal NormalB { get; }
            public Color BeginColor { get; }
            public Color EndColor { get; }
            public BoneWeight BeginBoneWeight { get; internal set; }
            public BoneWeight EndBoneWeight { get; internal set; }
            public BoneWeight LeftBoneWeight { get; internal set; }
            public BoneWeight RightBoneWeight { get; internal set; }
            public int Priority { get; }
            public bool NoSmooth { get; set; }
            public bool NoCull { get; set; }
            public bool NoFront { get; set; }
            //
            internal Wire(
                int subMeshIndex,
                int idxA, int idxB,
                int idxC, int idxD,
                Position vtxA, Position vtxB,
                Position left, Position right,
                Normal nmlA, Normal nmlB,
                Color colA, Color colB,
                int priority
            )
            {
                SubMeshIndex = subMeshIndex;
                BeginIndex = idxA;
                EndIndex = idxB;
                LeftIndex = idxC;
                RightIndex = idxD;
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
        }

        private readonly TriangleProvider _provider;
        private List<Wire> _wires = new List<Wire>();
        private List<Position> _positions = new List<Position>();
        private List<BoneWeight> _boneWeights = new List<BoneWeight>();
        private Dictionary<TriangleEdgeId, int> _wireMap = new Dictionary<TriangleEdgeId, int>();
        private Dictionary<int, int> _positionMap = new Dictionary<int, int>();
        public bool DestroyUselessWire = true;

        public FrameConstructor(TriangleProvider provider)
        {
            _provider = provider;
        }

        public void Construct()
        {
            _wires.Clear();
            _positions.Clear();
            _boneWeights.Clear();
            _wireMap.Clear();
            _positionMap.Clear();
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
            var (idxA, idxB) = triangle.GetIndices(edge);
            var (posA, posB) = triangle.GetPositions(edge);

            // edge.Next.First は edge.Second と恒等になってしまうため、3番目の頂点は edge.Second.Next で取る
            var idxC = triangle.GetIndex(edge.Second.Next);
            var posC = triangle.GetPosition(edge.Second.Next);

            var selfNormal = (nocull)? Normal.Invalid : triangle.FaceNormal;
            var sideTriangle = _provider.FindAdjacentTriangle(triangle, edge);
            if (!nocull && sideTriangle == null) return 0;

            var idxD = -1;
            var posD = default(Position);
            var otherNormal = Normal.Invalid;
            var sideEdge = default(TriangleEdge);
            if (sideTriangle != null)
            {
                sideEdge = sideTriangle.FindEdgeWithId(id);
                idxD = sideTriangle.GetIndex(sideEdge.Second.Next);
                posD = sideTriangle.GetPosition(sideEdge.Second.Next);
                otherNormal = sideTriangle.FaceNormal;
            }

            var colors = triangle.GetColors(edge);

            var selfSubMesh = triangle.SubMeshIndex;
            var otherSubMesh = (sideTriangle == null)? -1 : sideTriangle.SubMeshIndex;
            var otherIsFin = (sideTriangle == null)? false : sideTriangle.IsFin;

            if (
                !nocull &&
                DestroyUselessWire &&
                selfNormal == otherNormal &&
                selfSubMesh == otherSubMesh &&
                !otherIsFin
            ) return 0;

            var nmlA = selfNormal;
            var nmlB = otherNormal;
            var wire = new Wire(
                smi,
                idxA, idxB, idxC, idxD,
                posA, posB, posC, posD,
                nmlA, nmlB,
                colors.First, colors.Second,
                priority
            );

            if (triangle.HasBoneWeights)
            {
                wire.BeginBoneWeight = triangle.GetBoneWeight(edge.First);
                wire.EndBoneWeight = triangle.GetBoneWeight(edge.Second);
                wire.LeftBoneWeight = triangle.GetBoneWeight(edge.Second.Next);
            }
            if (sideTriangle != null && sideTriangle.HasBoneWeights)
            {
                wire.RightBoneWeight = sideTriangle.GetBoneWeight(sideEdge.Second.Next);
            }
            wire.NoCull = nocull;
            wire.NoSmooth = wire.NoFront = otherIsFin;
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
            int subMeshCount = SubMeshCount;
            var vb = new VertexBuffer(nline);
            for (int smi = 0; smi < subMeshCount; ++smi)
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
            int subMeshCount = SubMeshCount;
            var fs = ScriptableObject.CreateInstance<FrameStructure>();
            using (var editor = fs.BeginEdit())
            {
                for (int smi = 0; smi < subMeshCount; ++smi)
                {
                    using (var subMesh = editor.BeginCreateSubMesh())
                    {
                        for (var i = 0; i < nline; ++i)
                        {
                            var wire = _wires[i];
                            if (wire.SubMeshIndex != smi) continue;
                            subMesh.AddLine(
                                wire.BeginIndex,
                                wire.EndIndex,
                                wire.LeftIndex,
                                wire.RightIndex,
                                (Vector3)wire.BeginPosition,
                                (Vector3)wire.EndPosition,
                                (Vector3)wire.LeftPosition,
                                (Vector3)wire.RightPosition,
                                wire.BeginColor,
                                wire.EndColor,
                                wire.BeginBoneWeight,
                                wire.EndBoneWeight,
                                wire.LeftBoneWeight,
                                wire.RightBoneWeight,
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

