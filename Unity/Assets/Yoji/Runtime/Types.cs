using System;
using UnityEngine;

namespace Yoji
{
    [Serializable]
    public struct SubMesh
    {
        public int IndexStart;
        public int IndexCount;

        public SubMesh(int indexStart, int indexCount)
        {
            IndexStart = indexStart;
            IndexCount = indexCount;
        }
    }

    public struct SubMeshRange
    {
        public readonly int IndexStart;
        public readonly int IndexCount;

        public SubMeshRange(int indexStart, int indexCount)
        {
            IndexStart = indexStart;
            IndexCount = indexCount;
        }
    }

    public struct Position : IEquatable<Position>
    {
        private readonly Vector3 _value;

        public Position(Vector3 pos)
        {
            _value = pos;
        }

        public static explicit operator Vector3(Position pos)
        {
            return pos._value;
        }

        public bool Equals(Position b)
        {
            return _value == b._value;
        }
        public static bool operator ==(Position a, Position b) => a._value == b._value;
        public static bool operator !=(Position a, Position b) => a._value != b._value;
        public override bool Equals(object obj) => obj is Position position && Equals(position);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public struct Normal : IEquatable<Normal>
    {
        private readonly Vector3 _value;
        public static readonly Normal Invalid = new Normal(Vector3.zero);

        public Normal(Vector3 vec)
        {
            _value = vec;
        }

        public static explicit operator Vector3(Normal nml)
        {
            return nml._value;
        }

        public bool Equals(Normal b)
        {
            return _value == b._value;
        }
        public static bool operator ==(Normal a, Normal b) => a._value == b._value;
        public static bool operator !=(Normal a, Normal b) => a._value != b._value;
        public override bool Equals(object obj) => obj is Normal normal && Equals(normal);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
    }

    public struct Array3<T>
    {
        private T _a;
        private T _b;
        private T _c;

        public Array3(T a, T b, T c)
        {
            _a = a;
            _b = b;
            _c = c;
        }

        public T this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return _a;
                    case 1: return _b;
                    case 2: return _c;
                    default: throw new IndexOutOfRangeException();
                }
            }
            set
            {
                switch (index)
                {
                    case 0: _a = value; break;
                    case 1: _b = value; break;
                    case 2: _c = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }
    }
}
