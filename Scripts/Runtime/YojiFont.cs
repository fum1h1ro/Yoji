using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_EDITOR
using System.Linq;
#endif

namespace Yoji
{
    public class YojiFont : ScriptableObject
    {
        [System.Serializable]
        public struct FixedValue
        {
            const int ShiftSize = 10;
            const float Scale = (float)(1<<ShiftSize);
            const float InverseScale = 1.0f / Scale;
            public static readonly FixedValue Zero = new FixedValue(0.0f);
            public static readonly FixedValue MinValue = new FixedValue(-32768);
            public static readonly FixedValue MaxValue = new FixedValue(32767);
            [SerializeField] short _storage;

            public FixedValue(float v)
            {
                _storage = 0;
                Value = v;
            }

            internal FixedValue(short v)
            {
                _storage = v;
            }

            public float Value
            {
                get => (float)_storage * InverseScale;
                set => _storage = (short)(value * Scale);
            }

            internal short DirectValue
            {
                get => _storage;
                set => _storage = value;
            }
        }

        [System.Serializable]
        public struct Point
        {
            [SerializeField] FixedValue _x;
            [SerializeField] FixedValue _y;

            public Point(float x, float y)
            {
                _x = new FixedValue(x);
                _y = new FixedValue(y);
            }

            public float x
            {
                get { return _x.Value; }
                set { _x.Value = value; }
            }

            public float y
            {
                get { return _y.Value; }
                set { _y.Value = value; }
            }
        }

        [System.Serializable]
        public struct Table
        {
            const int OffsetWidth = 24;
            const int LengthWidth = 8;
            const int OffsetShift = 0;
            const int LengthShift = OffsetShift+OffsetWidth;
            const uint OffsetMask = (uint)((1<<OffsetWidth)-1) << OffsetShift;
            const uint LengthMask = (uint)((1<<LengthWidth)-1) << LengthShift;
            [SerializeField] uint _offsetAndLength;
            [SerializeField] FixedValue _left;
            [SerializeField] FixedValue _right;

            public Table(int offset, int length)
            {
                _offsetAndLength = 0U;
                _left = FixedValue.MaxValue;
                _right = FixedValue.MinValue;
                Offset = offset;
                Length = length;
            }

            public int Offset
            {
                get { return (int)((_offsetAndLength & OffsetMask) >> OffsetShift); }
                set {
                    _offsetAndLength = (uint)((_offsetAndLength & ~OffsetMask) | ((value << OffsetShift) & OffsetMask));
                }
            }

            public int Length
            {
                get { return (int)((_offsetAndLength & LengthMask) >> LengthShift); }
                set {
                    _offsetAndLength = (uint)((_offsetAndLength & ~LengthMask) | ((value << LengthShift) & LengthMask));
                }
            }

            public float Left
            {
                get { return _left.Value; }
                set { _left.Value = value; }
            }

            public float Right
            {
                get { return _right.Value; }
                set { _right.Value = value; }
            }

            public float Width
            {
                get { return _right.Value - _left.Value; }
            }
        }

        [SerializeField] FixedValue _Bottom = FixedValue.MaxValue;
        [SerializeField] FixedValue _Top = FixedValue.MinValue;
        [SerializeField] Table[] _Tables;
        [SerializeField] Point[] _Points;
        List<Table> _TableWork;
        List<Point> _PointWork;

#if UNITY_EDITOR
        public void BeginEdit()
        {
            Assert.IsNull(_TableWork);
            Assert.IsNull(_PointWork);
            _TableWork = (_Tables == null)? new List<Table>() : _Tables.ToList();
            _PointWork = (_Points == null)? new List<Point>() : _Points.ToList();
        }

        public void EndEdit()
        {
            Assert.IsNotNull(_TableWork);
            Assert.IsNotNull(_PointWork);
            _Tables = _TableWork.ToArray();
            _Points = _PointWork.ToArray();
            _TableWork = null;
            _PointWork = null;
        }

        public void SetTable(int code, int offset, int length)
        {
            Assert.IsNotNull(_TableWork);
            Assert.IsTrue(0 <= code && code <= 0xffff);

            while (_TableWork.Count <= code)
            {
                _TableWork.Add(new Table(0, 0));
            }

            var table = new Table(offset, length);
            var left = float.MaxValue;
            var right = float.MinValue;
            for (int i = 0; i < length; ++i)
            {
                left = Mathf.Min(left, _PointWork[offset+i].x);
                right = Mathf.Max(right, _PointWork[offset+i].x);
            }

            table.Left = 0.0f;
            table.Right = right - left;

            _TableWork[code] = table;

            for (int i = 0; i < length; ++i)
            {
                var pt = _PointWork[offset+i];
                pt.x -= left;
                _PointWork[offset+i] = pt;
            }
        }

        public void AddPoint(float x, float y)
        {
            Assert.IsNotNull(_PointWork);
            _PointWork.Add(new Point(x, y));
            _Bottom.Value = Mathf.Min(_Bottom.Value, y);
            _Top.Value = Mathf.Max(_Top.Value, y);
        }

        public int PointCount
        {
            get {
                Assert.IsNotNull(_PointWork);
                return _PointWork.Count;
            }
        }
#endif
        public Table Get(char c) => _Tables[System.Convert.ToInt32(c)];
        public Table Get(int code) => _Tables[code];
        public Point[] Points => _Points;
        public float Top => _Top.Value;
        public float Bottom => _Bottom.Value;
        public int Size => Mathf.CeilToInt(_Top.Value);
        public float Height => _Top.Value - _Bottom.Value;
    }
}
