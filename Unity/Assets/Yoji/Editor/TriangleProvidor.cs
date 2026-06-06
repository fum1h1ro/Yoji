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
        private readonly int[] PositionIndices = new int[TriangleVertex.Max];
        private readonly Vector3[] Positions = new Vector3[TriangleVertex.Max];
        private readonly Color32[] Colors = new Color32[TriangleVertex.Max];
        private BoneWeight[] BoneWeights = null;

        public int GetPositionIndex(TriangleVertex v) => PositionIndices[v];
        public (int, int) GetPositionIndices(TriangleEdge e) => (PositionIndices[e.First], PositionIndices[e.Second]);

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
            PositionIndices[TriangleVertex.A] = posIndexA;
            PositionIndices[TriangleVertex.B] = posIndexB;
            PositionIndices[TriangleVertex.C] = posIndexC;
            Positions[TriangleVertex.A] = posA;//_providor.PositionArray[posIndexA];
            Positions[TriangleVertex.B] = posB;//_providor.PositionArray[posIndexB];
            Positions[TriangleVertex.C] = posC;//_providor.PositionArray[posIndexC];
            Colors[TriangleVertex.A] = colA;
            Colors[TriangleVertex.B] = colB;
            Colors[TriangleVertex.C] = colC;
        }

        internal void AddBoneWeights(BoneWeight wgtA, BoneWeight wgtB, BoneWeight wgtC)
        {
            Assert.IsNull(BoneWeights);
            BoneWeights = new BoneWeight[TriangleVertex.Max];
            BoneWeights[TriangleVertex.A] = wgtA;
            BoneWeights[TriangleVertex.B] = wgtB;
            BoneWeights[TriangleVertex.C] = wgtC;
        }

        public Vector3 GetPosition(TriangleVertex v) => Positions[v];
        public (Vector3 First, Vector3 Second) GetPositions(TriangleEdge e) => (GetPosition(e.First), GetPosition(e.Second));
        public Color GetColor(TriangleVertex v) => Colors[v];
        public (Color First, Color Second) GetColors(TriangleEdge e) => (GetColor(e.First), GetColor(e.Second));
        public bool HasBoneWeights => BoneWeights != null;
        public BoneWeight GetBoneWeight(TriangleVertex v)
        {
            Assert.IsTrue(HasBoneWeights);
            return BoneWeights[v];
        }
        public (BoneWeight First, BoneWeight Second) GetBoneWeights(TriangleEdge e) => (GetBoneWeight(e.First), GetBoneWeight(e.Second));

        public float CalculateEdgeLength(TriangleEdge e)
        {
            var positions = GetPositions(e);
            return Vector3.Distance(positions.First, positions.Second);
        }

        public float[] CalculateEdgeLengths() => new float[]{ CalculateEdgeLength(TriangleEdge.AB), CalculateEdgeLength(TriangleEdge.BC), CalculateEdgeLength(TriangleEdge.CA) };

        public TriangleEdge FindLongestEdge()
        {
            var edgeLengths = CalculateEdgeLengths();
            return (TriangleEdge)((TriangleVertex)Array.FindIndex(edgeLengths, (len) => len == edgeLengths.Max()));
        }

        public Vector3 FaceNormal => Vector3.Cross((GetPosition(TriangleVertex.A) - GetPosition(TriangleVertex.B)).normalized, (GetPosition(TriangleVertex.A) - GetPosition(TriangleVertex.C)).normalized).normalized;

        public TriangleEdgeId GetEdgeID(TriangleEdge e) => new TriangleEdgeId(GetPositionIndex(e.First), GetPositionIndex(e.Second));

        public new string ToString()
        {
            return
                $"Index: {GetPositionIndex(TriangleVertex.A)}, {GetPositionIndex(TriangleVertex.B)}, {GetPositionIndex(TriangleVertex.C)}\n" +
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

    public class TriangleProvidor
    {
        private List<Triangle> Triangles = new List<Triangle>();
        private Dictionary<int, List<Triangle>> TrianglesWithSubmesh = new Dictionary<int, List<Triangle>>();
        private Dictionary<TriangleEdgeId, List<Triangle>> TrianglesWithEdge = new Dictionary<TriangleEdgeId, List<Triangle>>();
        private List<Vector3> Positions = new List<Vector3>();
        private List<Color> Colors = new List<Color>();
        private List<BoneWeight> BoneWeights = new List<BoneWeight>();

        public TriangleProvidor(Mesh mesh)
        {
            mesh.GetVertices(Positions);
            mesh.GetColors(Colors);
            mesh.GetBoneWeights(BoneWeights);

            var table = MakeTranslateTable();
            for (int smi = 0; smi < mesh.subMeshCount; ++smi)
            {
                var indices = mesh.GetTriangles(smi);
                for (int ti = 0; ti < indices.Length / 3; ++ti)
                {
                    int idxA = indices[ti*3+0];
                    int idxB = indices[ti*3+1];
                    int idxC = indices[ti*3+2];
                    int posA = (table[idxA] < 0)? idxA : table[idxA];
                    int posB = (table[idxB] < 0)? idxB : table[idxB];
                    int posC = (table[idxC] < 0)? idxC : table[idxC];
                    int colA = idxA;
                    int colB = idxB;
                    int colC = idxC;
                    Add(smi, posA, posB, posC, colA, colB, colC);
                }
            }
            //foreach (var t in Triangles) Debug.Log(t.ToString());
            SearchIndependentTriangles();
            SearchFinTriangles();
            Sort();
            foreach (var t in Triangles) Debug.Log(t.ToString());
        }

        // 同一座標の頂点をまとめるためのテーブルを作る
        private int[] MakeTranslateTable()
        {
            var table = new int[Positions.Count];
            table = table.Select(_ => -1).ToArray();
            for (int i = 0; i < Positions.Count; ++i)
            {
                if (table[i] >= 0) continue;
                for (int j = i+1; j < Positions.Count; ++j)
                {
                    if (table[j] >= 0) continue;
                    if (Positions[i] == Positions[j]) // approximately
                    {
                        table[j] = i;
                    }
                }
            }
            return table;
        }

        private Triangle Add(int subMeshIndex, int posA, int posB, int posC, int colA, int colB, int colC)
        {
            //Debug.Log($"{posA},{posB},{posC},{colA},{colB},{colC}");
            var tri = new Triangle(
                subMeshIndex,
                posA,
                posB,
                posC,
                Positions[posA],
                Positions[posB],
                Positions[posC],
                (HasColor)? Colors[colA] : Color.white,
                (HasColor)? Colors[colB] : Color.white,
                (HasColor)? Colors[colC] : Color.white
            );
            if (HasBoneWeights)
            {
                var wgtA = BoneWeights[posA];
                var wgtB = BoneWeights[posB];
                var wgtC = BoneWeights[posC];
                tri.AddBoneWeights(wgtA, wgtB, wgtC);
            }
            Triangles.Add(tri);

            if (!TrianglesWithSubmesh.ContainsKey(subMeshIndex))
            {
                TrianglesWithSubmesh[subMeshIndex] = new List<Triangle>();
            }
            TrianglesWithSubmesh[subMeshIndex].Add(tri);

            foreach (var TriangleEdge in TriangleEdge.Values)
            {
                var edgeHash = tri.GetEdgeID(TriangleEdge);
                if (!TrianglesWithEdge.ContainsKey(edgeHash)) TrianglesWithEdge[edgeHash] = new List<Triangle>();
                TrianglesWithEdge[edgeHash].Add(tri);
            }
            return tri;
        }

        private void Sort()
        {
            const int AisSmaller = -1;
            const int BisSmaller = 1;
            const int Same = 0;
            Triangles.Sort((a, b) =>
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
            foreach (var t in Triangles)
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
            foreach (var t in Triangles)
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

        public IReadOnlyList<Vector3> PositionArray => Positions;
        public IReadOnlyList<Color> ColorArray => Colors;
        public IReadOnlyList<BoneWeight> BoneWeightArray => BoneWeights;
        public IReadOnlyList<Triangle> AllTriangles => Triangles;
        public IReadOnlyList<Triangle> GetTriangles(int subMeshIndex) => TrianglesWithSubmesh[subMeshIndex];
        public bool HasColor => Colors.Count > 0;
        public bool HasBoneWeights => BoneWeights.Count > 0;

        public int CountTrianglesWithEdgeID(TriangleEdgeId id)
        {
            if (!TrianglesWithEdge.ContainsKey(id)) return 0;
            return TrianglesWithEdge[id].Count;
        }

        public List<Triangle> EnumerateTrianglesWithEdgeID(TriangleEdgeId id)
        {
            if (!TrianglesWithEdge.ContainsKey(id)) return new List<Triangle>(); // empty
            return TrianglesWithEdge[id];
        }

        public List<Triangle> EnumerateTrianglesWithEdge(int a, int b)
        {
            return EnumerateTrianglesWithEdgeID(new TriangleEdgeId(a, b));
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
            sb.Append($"TriangleProvidor:{GetHashCode()}\n");
            sb.Append($"  AllTriangles:{AllTriangles.Count}\n");
            foreach (var t in AllTriangles) {
                sb.Append(t.ToString());
            }
            return sb.ToString();
        }
    }
}

