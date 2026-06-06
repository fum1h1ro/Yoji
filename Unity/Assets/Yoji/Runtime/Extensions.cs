using UnityEngine;
using UnityEngine.Assertions;

namespace Yoji
{
    public static class Extensions
    {
        public static int GetBoneCount(this BoneWeight weight)
        {
            int count = 0;
            if (weight.weight0 > 0.0f) count++;
            if (weight.weight1 > 0.0f) count++;
            if (weight.weight2 > 0.0f) count++;
            if (weight.weight3 > 0.0f) count++;
            return count;
        }

        public static void Normalize(this BoneWeight weight, int maxBoneCount)
        {
            Assert.IsTrue(maxBoneCount > 0 && maxBoneCount <= 4, "maxBoneCount must be between 1 and 4");
            var boneCount = weight.GetBoneCount();
            if (boneCount <= maxBoneCount) return;

            if (maxBoneCount < 4)
            {
                if (maxBoneCount < 3) weight.weight2 = 0.0f;
                if (maxBoneCount < 2) weight.weight1 = 0.0f;
            }

            var totalWeight = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
            if (totalWeight > 0.0f)
            {
                var invTotal = 1.0f / totalWeight;
                weight.weight0 *= invTotal;
                weight.weight1 *= invTotal;
                weight.weight2 *= invTotal;
                weight.weight3 *= invTotal;
            }
            else
            {
                // If total weight is zero, assign full weight to the first bone
                weight.weight0 = 1.0f;
                weight.weight1 = 0.0f;
                weight.weight2 = 0.0f;
                weight.weight3 = 0.0f;
            }
        }
    }
}
