using NUnit.Framework;
using Assert = UnityEngine.Assertions.Assert;
using Yoji.Editor;

namespace Yoji.Tests
{
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
