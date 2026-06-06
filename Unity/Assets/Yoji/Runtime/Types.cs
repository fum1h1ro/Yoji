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

    public struct ReadonlySubMesh
    {
        public readonly int IndexStart;
        public readonly int IndexCount;

        public ReadonlySubMesh(int indexStart, int indexCount)
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
    }

    public struct Normal
    {
        private readonly Vector3 _value;

        public Normal(Vector3 vec)
        {
            _value = vec;
        }

        public static explicit operator Vector3(Normal nml)
        {
            return nml._value;
        }
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

        public T x
        {
            get => _a;
            set => _a = value;
        }
        public T y
        {
            get => _b;
            set => _b = value;
        }
        public T z
        {
            get => _c;
            set => _c = value;
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
