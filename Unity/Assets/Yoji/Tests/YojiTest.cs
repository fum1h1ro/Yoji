using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Assert = UnityEngine.Assertions.Assert;
using Yoji.Editor;

namespace Yoji.Tests
{
	public class YojiFontTest {
		[Test]
		public void YojiTestSimplePasses()
        {
			var r = new YojiFont.Table(0, 0);
			Assert.AreEqual(r.Offset, 0);
			Assert.AreEqual(r.Length, 0);
			r.Offset = 256;
			Assert.AreEqual(r.Offset, 256);
			r.Length = 128;
			Assert.AreEqual(r.Length, 128);
		}
		[Test]
		public void FixedValueTest()
        {
			Assert.AreApproximatelyEqual(0.0f, (new YojiFont.FixedValue(0.0f)).Value);
			Assert.AreApproximatelyEqual(0.5f, (new YojiFont.FixedValue(0.5f)).Value);
			Assert.AreApproximatelyEqual(1.5f, (new YojiFont.FixedValue(1.5f)).Value);
			Assert.AreNotApproximatelyEqual(0.0005f, (new YojiFont.FixedValue(0.0005f)).Value);
		}
	}
	public class YojiFloatUtilityTest
    {
		[Test]
		public void PackTest()
        {
			int[] targetBits = new int[]{ 8, 16 };
			int[] maskBits = new int[]{ 1, 2, 3, 4, 5, 6, 7 };
			foreach (var t in targetBits)
            {
				foreach (var m in maskBits)
                {
					EncodeAndDecode(0.5f, 1, m, t);
				}
			}
			EncodeAndDecode(0.5f, 1, 1, 8);
			EncodeAndDecode(0.5f, 2, 2, 8);
			EncodeAndDecode(0.5f, 15, 4, 8);
		}


		void EncodeAndDecode(float scalar, uint mask, int maskBitLength, int targetBitLength)
        {
			Debug.Log($"scalar:{scalar} mask:{mask} maskBit:{maskBitLength} targetBitLength:{targetBitLength}");
			var fval = Utility.PackFloatInt(scalar, mask, maskBitLength, targetBitLength);
			Utility.UnpackFloatInt(fval, maskBitLength, targetBitLength, out float s, out uint m);
			Assert.AreApproximatelyEqual(scalar, s);
			Assert.AreEqual(mask, m);
		}
	}
	public class TriangleProviderTest
    {
		[Test]
		public void VertexTest()
        {
			Assert.AreEqual(TriangleVertex.A.Next, TriangleVertex.B);
			Assert.AreEqual(TriangleVertex.B.Next, TriangleVertex.C);
			Assert.AreEqual(TriangleVertex.C.Next, TriangleVertex.A);
		}
		[Test]
		public void EdgeTest()
        {
			Assert.AreEqual(TriangleEdge.AB.Next, TriangleEdge.BC);
			Assert.AreEqual(TriangleEdge.BC.Next, TriangleEdge.CA);
			Assert.AreEqual(TriangleEdge.CA.Next, TriangleEdge.AB);
		}
	}
}

