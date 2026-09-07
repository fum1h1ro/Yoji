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
            private const int ShiftSize = 10;
            private const float Scale = (float)(1<<ShiftSize);
            private const float InverseScale = 1.0f / Scale;
            public static readonly FixedValue Zero = new FixedValue(0.0f);
            public static readonly FixedValue MinValue = new FixedValue(-32768);
            public static readonly FixedValue MaxValue = new FixedValue(32767);
            [SerializeField] private short _storage;

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
            [SerializeField] private FixedValue _x;
            [SerializeField] private FixedValue _y;

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
        public struct Glyph
        {
            private const int OffsetWidth = 24;
            private const int LengthWidth = 8;
            private const int OffsetShift = 0;
            private const int LengthShift = OffsetShift+OffsetWidth;
            private const uint OffsetMask = (uint)((1<<OffsetWidth)-1) << OffsetShift;
            private const uint LengthMask = (uint)((1<<LengthWidth)-1) << LengthShift;
            [SerializeField] private uint _offsetAndLength;
            [SerializeField] private FixedValue _left;
            [SerializeField] private FixedValue _right;

            public Glyph(int offset, int length)
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

        [SerializeField] private FixedValue _bottom = FixedValue.MaxValue;
        [SerializeField] private FixedValue _top = FixedValue.MinValue;
        [SerializeField] private Glyph[] _glyphs;
        [SerializeField] private Point[] _points;
        private List<Glyph> _glyphWork;
        private List<Point> _pointWork;

#if UNITY_EDITOR
        public void BeginEdit()
        {
            Assert.IsNull(_glyphWork);
            Assert.IsNull(_pointWork);
            _glyphWork = (_glyphs == null)? new List<Glyph>() : _glyphs.ToList();
            _pointWork = (_points == null)? new List<Point>() : _points.ToList();
        }

        public void EndEdit()
        {
            Assert.IsNotNull(_glyphWork);
            Assert.IsNotNull(_pointWork);
            _glyphs = _glyphWork.ToArray();
            _points = _pointWork.ToArray();
            _glyphWork = null;
            _pointWork = null;
        }

        public void SetGlyph(int code, int offset, int length)
        {
            Assert.IsNotNull(_glyphWork);
            Assert.IsTrue(0 <= code && code <= 0xffff);

            while (_glyphWork.Count <= code)
            {
                _glyphWork.Add(new Glyph(0, 0));
            }

            var glyph = new Glyph(offset, length);
            var left = float.MaxValue;
            var right = float.MinValue;
            for (int i = 0; i < length; ++i)
            {
                left = Mathf.Min(left, _pointWork[offset+i].x);
                right = Mathf.Max(right, _pointWork[offset+i].x);
            }

            glyph.Left = 0.0f;
            glyph.Right = right - left;

            _glyphWork[code] = glyph;

            for (int i = 0; i < length; ++i)
            {
                var pt = _pointWork[offset+i];
                pt.x -= left;
                _pointWork[offset+i] = pt;
            }
        }

        public void AddPoint(float x, float y)
        {
            Assert.IsNotNull(_pointWork);
            _pointWork.Add(new Point(x, y));
            _bottom.Value = Mathf.Min(_bottom.Value, y);
            _top.Value = Mathf.Max(_top.Value, y);
        }

        public int PointCount
        {
            get {
                Assert.IsNotNull(_pointWork);
                return _pointWork.Count;
            }
        }
#endif
        public Glyph Get(char c) => _glyphs[System.Convert.ToInt32(c)];
        public Glyph Get(int code) => _glyphs[code];
        public Point[] Points => _points;
        public float Top => _top.Value;
        public float Bottom => _bottom.Value;
        public int Size => Mathf.CeilToInt(_top.Value);
        public float Height => _top.Value - _bottom.Value;
    }
}
