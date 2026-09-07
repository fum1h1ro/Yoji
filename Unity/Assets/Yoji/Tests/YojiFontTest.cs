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
		public void GlyphOffsetLengthTest()
        {
			var r = new YojiFont.Glyph(0, 0);
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
}

