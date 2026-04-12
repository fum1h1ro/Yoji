using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Yoji.Editor
{
    public class TriangleProvidor : IDisposable
    {
        public struct Vertex
        {
            private int Value;

            private Vertex(int v)
            {
                while (v < 0) v += Max;
                Value = v % Max;
            }

            public static Vertex A = new Vertex(0);
            public static Vertex B = new Vertex(1);
            public static Vertex C = new Vertex(2);
            public static Vertex[] Values = new Vertex[]{ Vertex.A, Vertex.B, Vertex.C };
            public const int Max = 3;

            public Vertex Add(int offset) => (Vertex)(Value + offset);
            public Vertex Next => Add(1);

            public static explicit operator Vertex(int i) => new Vertex(i);
            public static implicit operator int(Vertex v) => v.Value;
        }

        public struct Edge
        {
            private int Value;

            private Edge(Vertex a, Vertex b)
            {
                Value = ((a & 0xff) << 16) | (b & 0xff);
            }

            public static Edge AB = new Edge(Vertex.A, Vertex.B);
            public static Edge BC = new Edge(Vertex.B, Vertex.C);
            public static Edge CA = new Edge(Vertex.C, Vertex.A);
            public static Edge[] Values = new Edge[]{ Edge.AB, Edge.BC, Edge.CA };
            public const int Max = 3;

            public Vertex First => (Vertex)((Value >> 16) & 0xff);
            public Vertex Second => (Vertex)(Value & 0xff);
            public Edge Next => (Edge)Second;

            public static explicit operator Edge(Vertex v) => new Edge(v, v.Next);
        }

        public struct EdgeID
        {
            public readonly long Value;

            public EdgeID(int a, int b)
            {
                if (a < b)
                    Value = CalcHash(a, b);
                else
                    Value = CalcHash(b, a);
            }

            public int First => (int)((Value & 0x7fffffff00000000) >> 32);
            public int Second => (int)(Value & 0x000000007fffffff);
            static long CalcHash(int a, int b) => ((long)a << 32) | (long)b;
            public override string ToString() => $"EdgeID:({First}, {Second})";
        }

        public class Triangle
        {
            private readonly TriangleProvidor Providor;
            public readonly int SubMeshIndex;
            private readonly int[] PositionIndices = new int[Vertex.Max];
            private readonly int[] ColorIndices = new int[Vertex.Max];

            public int GetPositionIndex(Vertex v) => PositionIndices[v];
            public (int, int) GetPositionIndices(Edge e) => (PositionIndices[e.First], PositionIndices[e.Second]);
            public int GetColorIndex(Vertex v) => ColorIndices[v];
            public (int, int) GetColorIndices(Edge e) => (ColorIndices[e.First], ColorIndices[e.Second]);

            internal Triangle(TriangleProvidor providor, int submesh, int posA, int posB, int posC, int colA, int colB, int colC)
            {
                Providor = providor;
                SubMeshIndex = submesh;
                PositionIndices[Vertex.A] = posA;
                PositionIndices[Vertex.B] = posB;
                PositionIndices[Vertex.C] = posC;
                ColorIndices[Vertex.A] = colA;
                ColorIndices[Vertex.B] = colB;
                ColorIndices[Vertex.C] = colC;
            }

            public Vector3 GetPosition(Vertex v) => Providor.PositionArray[GetPositionIndex(v)];
            public (Vector3 First, Vector3 Second) GetPositions(Edge e) => (GetPosition(e.First), GetPosition(e.Second));
            public Color GetColor(Vertex v)
            {
                if (!Providor.HasColor) return Color.white;
                return Providor.ColorArray[GetColorIndex(v)];
            }
            public (Color First, Color Second) GetColors(Edge e) => (GetColor(e.First), GetColor(e.Second));

            // Edgeを共有している三角形
            public Triangle GetOtherTriangle(Edge e)
            {
                var triangles = Providor.EnumerateTrianglesWithEdgeID(GetEdgeID(e)).Where((t) => t != this).ToArray();
                return (triangles.Length == 0)? null : triangles[0];
            }

            public float CalculateEdgeLength(Edge e)
            {
                var positions = GetPositions(e);
                return Vector3.Distance(positions.First, positions.Second);
            }

            public float[] CalculateEdgeLengths() => new float[]{ CalculateEdgeLength(Edge.AB), CalculateEdgeLength(Edge.BC), CalculateEdgeLength(Edge.CA) };

            public Edge FindLongestEdge()
            {
                var edgeLengths = CalculateEdgeLengths();
                return (Edge)((Vertex)Array.FindIndex(edgeLengths, (len) => len == edgeLengths.Max()));
            }

            public Vector3 FaceNormal => Vector3.Cross((GetPosition(Vertex.A) - GetPosition(Vertex.B)).normalized, (GetPosition(Vertex.A) - GetPosition(Vertex.C)).normalized).normalized;

            public EdgeID GetEdgeID(Edge e) => new EdgeID(GetPositionIndex(e.First), GetPositionIndex(e.Second));

            // すべての辺を他の三角形と共有していない？
            public bool IsIndependent
            {
                get
                {
                    foreach (var edge in Edge.Values)
                    {
                        var edgeID = GetEdgeID(edge);
                        if (Providor.CountTrianglesWithEdgeID(edgeID) > 1)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            // 一辺のみ共有している状態か？
            public bool IsFin
            {
                get
                {
                    int sharedCount = 0;
                    foreach (var edge in Edge.Values)
                    {
                        var edgeID = GetEdgeID(edge);
                        if (Providor.CountTrianglesWithEdgeID(edgeID) > 1)
                        {
                            ++sharedCount;
                        }
                    }
                    return !IsIndependent && sharedCount <= 1;
                }
            }

            public new string ToString()
            {
                return
                    $"Index: {GetPositionIndex(Vertex.A)}, {GetPositionIndex(Vertex.B)}, {GetPositionIndex(Vertex.C)}\n" +
                    $"Position: {GetPosition(Vertex.A)}, {GetPosition(Vertex.B)}, {GetPosition(Vertex.C)}\n" +
                    $"Color: {GetColor(Vertex.A)}, {GetColor(Vertex.B)}, {GetColor(Vertex.C)}\n" +
                    $"Normal: {FaceNormal}\n" +
                    $"Longest: {FindLongestEdge()}: {CalculateEdgeLengths()[FindLongestEdge().First]}\n" +
                    $"EdgeID: {GetEdgeID(Edge.AB)}-{GetEdgeID(Edge.BC)}-{GetEdgeID(Edge.CA)}\n" +
                    $"SubMeshIndex: {SubMeshIndex}\n" +
                    $"IsIndependent: {IsIndependent}\n" +
                    $"IsFin: {IsFin}\n";
            }
        }

        private List<Triangle> Triangles = new List<Triangle>();
        private Dictionary<int, List<Triangle>> TrianglesWithSubmesh = new Dictionary<int, List<Triangle>>();
        private Dictionary<EdgeID, List<Triangle>> TrianglesWithEdge = new Dictionary<EdgeID, List<Triangle>>();
        private List<Vector3> Positions = new List<Vector3>();
        private List<Color> Colors = new List<Color>();

        public TriangleProvidor(Mesh mesh)
        {
            mesh.GetVertices(Positions);
            mesh.GetColors(Colors);
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
            Sort();
            foreach (var t in Triangles) Debug.Log(t.ToString());
        }

        public void Dispose()
        {
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

        private void Add(int subMeshIndex, int posA, int posB, int posC, int colA, int colB, int colC)
        {
            //Debug.Log($"{posA},{posB},{posC},{colA},{colB},{colC}");
            var tri = new Triangle(this, subMeshIndex, posA, posB, posC, colA, colB, colC);
            Triangles.Add(tri);

            if (!TrianglesWithSubmesh.ContainsKey(subMeshIndex))
            {
                TrianglesWithSubmesh[subMeshIndex] = new List<Triangle>();
            }
            TrianglesWithSubmesh[subMeshIndex].Add(tri);

            foreach (var edge in Edge.Values)
            {
                var edgeHash = tri.GetEdgeID(edge);
                if (!TrianglesWithEdge.ContainsKey(edgeHash)) TrianglesWithEdge[edgeHash] = new List<Triangle>();
                TrianglesWithEdge[edgeHash].Add(tri);
            }
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

        public IReadOnlyList<Vector3> PositionArray => Positions;
        public IReadOnlyList<Color> ColorArray => Colors;
        public IReadOnlyList<Triangle> AllTriangles => Triangles;
        public IReadOnlyList<Triangle> GetTriangles(int subMeshIndex) => TrianglesWithSubmesh[subMeshIndex];
        public bool HasColor => Colors.Count > 0;

        public int CountTrianglesWithEdgeID(EdgeID id)
        {
            if (!TrianglesWithEdge.ContainsKey(id)) return 0;
            return TrianglesWithEdge[id].Count;
        }

        public List<Triangle> EnumerateTrianglesWithEdgeID(EdgeID id)
        {
            if (!TrianglesWithEdge.ContainsKey(id)) return new List<Triangle>(); // empty
            return TrianglesWithEdge[id];
        }

        public List<Triangle> EnumerateTrianglesWithEdge(int a, int b)
        {
            return EnumerateTrianglesWithEdgeID(new EdgeID(a, b));
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

