using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Mathematics;

namespace Yoji.Editor
{
    public struct TriangleVertex
    {
        private int _value;

        private TriangleVertex(int v)
        {
            while (v < 0) v += Max;
            _value = v % Max;
        }

        public static readonly TriangleVertex A = new TriangleVertex(0);
        public static readonly TriangleVertex B = new TriangleVertex(1);
        public static readonly TriangleVertex C = new TriangleVertex(2);
        public static readonly TriangleVertex[] Values = new TriangleVertex[]{ TriangleVertex.A, TriangleVertex.B, TriangleVertex.C };
        public const int Max = 3;

        public TriangleVertex Add(int offset) => new TriangleVertex(_value + offset);
        public TriangleVertex Next => Add(1);

        public static explicit operator TriangleVertex(int i) => new TriangleVertex(i);
        public static implicit operator int(TriangleVertex v) => v._value;
    }

    public struct TriangleEdge
    {
        private int _value;

        private TriangleEdge(TriangleVertex a, TriangleVertex b)
        {
            _value = ((a & 0xff) << 16) | (b & 0xff);
        }

        public static readonly TriangleEdge AB = new TriangleEdge(TriangleVertex.A, TriangleVertex.B);
        public static readonly TriangleEdge BC = new TriangleEdge(TriangleVertex.B, TriangleVertex.C);
        public static readonly TriangleEdge CA = new TriangleEdge(TriangleVertex.C, TriangleVertex.A);
        public static readonly TriangleEdge[] Values = new TriangleEdge[]{ TriangleEdge.AB, TriangleEdge.BC, TriangleEdge.CA };
        public const int Max = 3;

        public TriangleVertex First => (TriangleVertex)((_value >> 16) & 0xff);
        public TriangleVertex Second => (TriangleVertex)(_value & 0xff);
        public TriangleEdge Next => (TriangleEdge)Second;

        public static explicit operator TriangleEdge(TriangleVertex v) => new TriangleEdge(v, v.Next);
    }

    public struct TriangleEdgeId
    {
        public readonly long Value;

        public TriangleEdgeId(int a, int b)
        {
            if (a < b)
                Value = MakeId(a, b);
            else
                Value = MakeId(b, a);
        }

        public int First => (int)((Value & 0x7fffffff00000000) >> 32);
        public int Second => (int)(Value & 0x000000007fffffff);
        private static long MakeId(int a, int b) => ((long)a << 32) | (long)b;
        public override string ToString() => $"TriangleEdgeId:({First}, {Second})";
    }

    public class Triangle
    {
        public readonly int SubMeshIndex;
        private readonly Array3<int> _indices;
        private readonly Array3<Position> _positions;
        private readonly Array3<Color32> _colors;
        private Array3<BoneWeight> _boneWeights;
        public bool HasBoneWeights { get; private set; } = false;

        public int GetIndex(TriangleVertex v) => _indices[v];
        public (int, int) GetIndices(TriangleEdge e) => (_indices[e.First], _indices[e.Second]);

        public bool IsIndependent { get; internal set; }
        public bool IsFin { get; internal set; }

        internal Triangle(
            int submesh,
            int posIndexA,
            int posIndexB,
            int posIndexC,
            Vector3 posA,
            Vector3 posB,
            Vector3 posC,
            Color32 colA,
            Color32 colB,
            Color32 colC
        )
        {
            SubMeshIndex = submesh;
            _indices = new(posIndexA, posIndexB, posIndexC);
            _positions = new(new Position(posA), new Position(posB), new Position(posC));
            _colors = new(colA, colB, colC);
        }

        internal void AddBoneWeights(BoneWeight wgtA, BoneWeight wgtB, BoneWeight wgtC)
        {
            Assert.IsFalse(HasBoneWeights);
            _boneWeights = new(wgtA, wgtB, wgtC);
            HasBoneWeights = true;
        }

        public Position GetPosition(TriangleVertex v) => _positions[v];
        public (Position First, Position Second) GetPositions(TriangleEdge e) => (GetPosition(e.First), GetPosition(e.Second));
        public Color GetColor(TriangleVertex v) => _colors[v];
        public (Color First, Color Second) GetColors(TriangleEdge e) => (GetColor(e.First), GetColor(e.Second));
        public BoneWeight GetBoneWeight(TriangleVertex v)
        {
            Assert.IsTrue(HasBoneWeights);
            return _boneWeights[v];
        }
        public (BoneWeight First, BoneWeight Second) GetBoneWeights(TriangleEdge e) => (GetBoneWeight(e.First), GetBoneWeight(e.Second));

        public float CalculateEdgeLength(TriangleEdge e)
        {
            var positions = GetPositions(e);
            return Vector3.Distance((Vector3)positions.First, (Vector3)positions.Second);
        }

        public float[] CalculateEdgeLengths() => new float[]{ CalculateEdgeLength(TriangleEdge.AB), CalculateEdgeLength(TriangleEdge.BC), CalculateEdgeLength(TriangleEdge.CA) };

        public TriangleEdge FindLongestEdge()
        {
            var edgeLengths = CalculateEdgeLengths();
            return (TriangleEdge)((TriangleVertex)Array.FindIndex(edgeLengths, (len) => len == edgeLengths.Max()));
        }
        public TriangleEdge FindEdgeWithId(TriangleEdgeId id)
        {
            var edges = TriangleEdge.Values;
            foreach (var edge in edges)
            {
                var edgeId = GetEdgeID(edge);
                if (edgeId.Value == id.Value) return edge;
            }
            throw new Exception($"Triangle.FindEdgeId: Not found. id={id}");
        }

        public Normal FaceNormal =>
            new Normal(Vector3.Cross(
                ((Vector3)GetPosition(TriangleVertex.A) - (Vector3)GetPosition(TriangleVertex.B)).normalized,
                ((Vector3)GetPosition(TriangleVertex.A) - (Vector3)GetPosition(TriangleVertex.C)).normalized
            ).normalized);

        public TriangleEdgeId GetEdgeID(TriangleEdge e) => new TriangleEdgeId(GetIndex(e.First), GetIndex(e.Second));

        public new string ToString()
        {
            return
                $"Index: {GetIndex(TriangleVertex.A)}, {GetIndex(TriangleVertex.B)}, {GetIndex(TriangleVertex.C)}\n" +
                $"Position: {GetPosition(TriangleVertex.A)}, {GetPosition(TriangleVertex.B)}, {GetPosition(TriangleVertex.C)}\n" +
                $"Color: {GetColor(TriangleVertex.A)}, {GetColor(TriangleVertex.B)}, {GetColor(TriangleVertex.C)}\n" +
                $"BoneWeights: {(HasBoneWeights? $"{GetBoneWeight(TriangleVertex.A)}, {GetBoneWeight(TriangleVertex.B)}, {GetBoneWeight(TriangleVertex.C)}" : "None")}\n" +
                $"Normal: {FaceNormal}\n" +
                $"Longest: {FindLongestEdge()}: {CalculateEdgeLengths()[FindLongestEdge().First]}\n" +
                $"TriangleEdgeId: {GetEdgeID(TriangleEdge.AB)}-{GetEdgeID(TriangleEdge.BC)}-{GetEdgeID(TriangleEdge.CA)}\n" +
                $"SubMeshIndex: {SubMeshIndex}\n" +
                $"IsIndependent: {IsIndependent}\n" +
                $"IsFin: {IsFin}\n";
        }
    }

    public class TriangleProvider
    {
        private List<Triangle> _triangles = new List<Triangle>();
        private Dictionary<TriangleEdgeId, List<Triangle>> _trianglesWithEdge = new Dictionary<TriangleEdgeId, List<Triangle>>();
        private List<Vector3> _positions = new List<Vector3>();
        private List<Color> _colors = new List<Color>();
        private List<BoneWeight> _boneWeights = new List<BoneWeight>();

        public TriangleProvider(Mesh mesh)
        {
            var posTemp = new List<Vector3>();

            mesh.GetVertices(posTemp);
            mesh.GetColors(_colors);
            mesh.GetBoneWeights(_boneWeights);

            var posCache = new Dictionary<Vector3, int>(posTemp.Count);
            _positions.Capacity = posTemp.Count;

            int GetOrAddPosition(Vector3 pos)
            {
                if (posCache.TryGetValue(pos, out var index)) return index;
                index = _positions.Count;
                _positions.Add(pos);
                posCache[pos] = index;
                return index;
            }

            for (int smi = 0; smi < mesh.subMeshCount; ++smi)
            {
                var indices = mesh.GetTriangles(smi);
                for (int ti = 0; ti < indices.Length / 3; ++ti)
                {
                    int idxA = indices[ti*3+0];
                    int idxB = indices[ti*3+1];
                    int idxC = indices[ti*3+2];

                    int posIdxA = GetOrAddPosition(posTemp[idxA]);
                    int posIdxB = GetOrAddPosition(posTemp[idxB]);
                    int posIdxC = GetOrAddPosition(posTemp[idxC]);

                    Add(smi, posIdxA, posIdxB, posIdxC, idxA, idxB, idxC);
                }
            }
            //foreach (var t in _triangles) Debug.Log(t.ToString());
            SearchIndependentTriangles();
            SearchFinTriangles();
            Sort();
            foreach (var t in _triangles) Debug.Log(t.ToString());
        }

        private Triangle Add(int subMeshIndex, int posIdxA, int posIdxB, int posIdxC, int vtxIdxA, int vtxIdxB, int vtxIdxC)
        {
            var tri = new Triangle(
                subMeshIndex,
                posIdxA,
                posIdxB,
                posIdxC,
                _positions[posIdxA],
                _positions[posIdxB],
                _positions[posIdxC],
                (HasColor)? _colors[vtxIdxA] : Color.white,
                (HasColor)? _colors[vtxIdxB] : Color.white,
                (HasColor)? _colors[vtxIdxC] : Color.white
            );
            if (HasBoneWeights)
            {
                var wgtA = _boneWeights[vtxIdxA];
                var wgtB = _boneWeights[vtxIdxB];
                var wgtC = _boneWeights[vtxIdxC];
                tri.AddBoneWeights(wgtA, wgtB, wgtC);
            }
            _triangles.Add(tri);

            foreach (var edge in TriangleEdge.Values)
            {
                var edgeHash = tri.GetEdgeID(edge);
                if (!_trianglesWithEdge.ContainsKey(edgeHash)) _trianglesWithEdge[edgeHash] = new List<Triangle>();
                _trianglesWithEdge[edgeHash].Add(tri);
            }
            return tri;
        }

        private void Sort()
        {
            const int AisSmaller = -1;
            const int BisSmaller = 1;
            const int Same = 0;
            _triangles.Sort((a, b) =>
            {
                if (a.SubMeshIndex < b.SubMeshIndex) return AisSmaller;
                if (a.SubMeshIndex > b.SubMeshIndex) return BisSmaller;
                //if (a.IsIndependent) return AisSmaller;
                //if (b.IsIndependent) return BisSmaller;
                //if (a.IsFin) return BisSmaller;
                //if (b.IsFin) return AisSmaller;
                return Same;
            });
        }

        private void SearchIndependentTriangles()
        {
            foreach (var t in _triangles)
            {
                var count = 0;
                foreach (var edge in TriangleEdge.Values)
                {
                    var edgeID = t.GetEdgeID(edge);
                    count = math.max(count, CountTrianglesWithEdgeID(edgeID));
                }
                t.IsIndependent = !(count > 1);
            }
        }

        private void SearchFinTriangles()
        {
            foreach (var t in _triangles)
            {
                int sharedCount = 0;
                foreach (var edge in TriangleEdge.Values)
                {
                    var edgeID = t.GetEdgeID(edge);
                    if (CountTrianglesWithEdgeID(edgeID) > 1)
                    {
                        ++sharedCount;
                    }
                }
                t.IsFin = !t.IsIndependent && sharedCount <= 1;
            }
        }

        public IReadOnlyList<Triangle> AllTriangles => _triangles;
        public bool HasColor => _colors.Count > 0;
        public bool HasBoneWeights => _boneWeights.Count > 0;

        public int CountTrianglesWithEdgeID(TriangleEdgeId id)
        {
            if (!_trianglesWithEdge.ContainsKey(id)) return 0;
            return _trianglesWithEdge[id].Count;
        }

        public List<Triangle> EnumerateTrianglesWithEdgeID(TriangleEdgeId id)
        {
            if (!_trianglesWithEdge.ContainsKey(id)) return new List<Triangle>(); // empty
            return _trianglesWithEdge[id];
        }

        // TriangleEdgeを共有している三角形
        public Triangle FindAdjacentTriangle(Triangle tri, TriangleEdge e)
        {
            var triangles = EnumerateTrianglesWithEdgeID(tri.GetEdgeID(e)).Where((t) => t != tri).ToArray();
            return (triangles.Length == 0)? null : triangles[0];
        }

        public new string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"TriangleProvider:{GetHashCode()}\n");
            sb.Append($"  AllTriangles:{AllTriangles.Count}\n");
            foreach (var t in AllTriangles) {
                sb.Append(t.ToString());
            }
            return sb.ToString();
        }
    }
}
