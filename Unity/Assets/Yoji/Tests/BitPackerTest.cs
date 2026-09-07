using UnityEngine;
using NUnit.Framework;
using Assert = UnityEngine.Assertions.Assert;

namespace Yoji.Tests
{
	public class BitPackerTest
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
			var fval = BitPacker.PackFloatInt(scalar, mask, maskBitLength, targetBitLength);
			BitPacker.UnpackFloatInt(fval, maskBitLength, targetBitLength, out float s, out uint m);
			Assert.AreApproximatelyEqual(scalar, s);
			Assert.AreEqual(mask, m);
		}
	}
}
