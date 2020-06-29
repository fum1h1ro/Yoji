using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yoji {
	public static class Utility {
		public static float PackFloatInt(float f, uint i, int numBitI, int numBitTarget) {
			// Constant optimize by compiler
			float precision = (float)(1 << numBitTarget);
			float maxi = (float)(1 << numBitI);
			float precisionMinusOne = precision - 1.0f;
			float t1 = ((precision / maxi) - 1.0f) / precisionMinusOne;
			float t2 = (precision / maxi) / precisionMinusOne;
			// Code
			return t1 * f + t2 * (float)i;
		}

		public static void UnpackFloatInt(float val, int numBitI, int numBitTarget, out float f, out uint i) {
			// Constant optimize by compiler
			float precision = (float)(1 << numBitTarget);
			float maxi = (float)(1 << numBitI);
			float precisionMinusOne = precision - 1.0f;
			float t1 = ((precision / maxi) - 1.0f) / precisionMinusOne;
			float t2 = (precision / maxi) / precisionMinusOne;
			// Code
			// extract integer part
			// + rcp(precisionMinusOne) to deal with precision issue
			i = (uint)((val / t2) + (1.0f / precisionMinusOne));
			// Now that we have i, solve formula in PackFloatInt for f
			//f = (val - t2 * float(i)) / t1 => convert in mads form
			f = Mathf.Clamp((-t2 * (float)i + val) / t1, 0.0f, 1.0f); // Saturate in case of precision issue
		}
	}
}
