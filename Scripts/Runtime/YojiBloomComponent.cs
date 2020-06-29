using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Yoji {
	[ExecuteInEditMode()]
	[RequireComponent(typeof(Camera))]
	public class YojiBloomComponent : MonoBehaviour {
		const int ShaderPass_Blend = 1;
		const int ShaderPass_GaussBlurVertical = 2;
		const int ShaderPass_GaussBlurHorizontal = 3;
		const int ShaderPass_ScaleAndOverwrite = 6;
		const int ShaderPass_GammaBlend = 7;
		RenderTextureDescriptor _desc;
		Material _material;
		static Shader _shader;
		[Range(1, 8)]
		[SerializeField] int _ShrinkLevel = 4;
		[Range(1, 8)]
		[SerializeField] int _GaussLevel = 4;
		//
		void OnEnable() {
			if (_shader == null) {
				_shader = Shader.Find("Hidden/Yoji/Bloom");
			}
			CreateMaterial();
		}
		void OnDisable() {
			DestroyMaterial();
		}
		void OnRenderImage(RenderTexture src, RenderTexture dst) {
			_material.SetFloat("_Scale", 2.0f); //@todo
			Graphics.Blit(src, dst, _material, ShaderPass_ScaleAndOverwrite);
			MakeShrink(src, dst);
		}
		// kawase's MGF
		void MakeShrink(RenderTexture src, RenderTexture dst) {
			int w = src.width;
			int h = src.height;
			var glareBuffer = AllocRT(w, h, FilterMode.Bilinear);

			float alpha = 1.0f;
			var previousBuffer = src;
			for (int i = 0; i < _ShrinkLevel; ++i) {
				int sw = previousBuffer.width;
				int sh = previousBuffer.height;
				bool shrinkDir = (i & 1) == 0;
				if (shrinkDir) {
					sh >>= 1;
				} else {
					sw >>= 1;
				}
				//Debug.Log($"{i}:{sw}x{sh}");
				var tempBuffer = AllocRT(sw, sh, FilterMode.Bilinear);
				_material.SetVector("_MainTex_TexelSize", new Vector4(1.0f / previousBuffer.width, 1.0f / previousBuffer.height, 0.0f, 0.0f));

				for (int j = 0; j < _GaussLevel; ++j) {
					if (shrinkDir) {
						Graphics.Blit(previousBuffer, tempBuffer, _material, ShaderPass_GaussBlurHorizontal);
					} else {
						Graphics.Blit(previousBuffer, tempBuffer, _material, ShaderPass_GaussBlurVertical);
					}
				}

				_material.SetInt("_BlendDst", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); //@todo
				_material.SetFloat("_BlendAlpha", alpha); //@todo
				Graphics.Blit(tempBuffer, glareBuffer, _material, ShaderPass_Blend);
				alpha *= 0.5f;

				if (i != 0) {
					FreeRT(previousBuffer);
				}
				previousBuffer = tempBuffer;
			}
			FreeRT(previousBuffer);


			_material.SetInt("_BlendDst", (int)UnityEngine.Rendering.BlendMode.One); //@todo
			//_material.SetInt("_BlendDst", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); //@todo
			_material.SetFloat("_Scale", 2.0f); //@todo
			_material.SetFloat("_BlendAlpha", 1.0f); //@todo
			Graphics.Blit(glareBuffer, dst, _material, ShaderPass_GammaBlend);
			FreeRT(glareBuffer);
		}
		void CreateMaterial() {
			if (_material == null) {
				_material = new Material(_shader);
				var x = 1.0f / Screen.width;
				var y = 1.0f / Screen.height;
				_material.SetVector("_MainTex_TexelSize", new Vector4(x, y, 0.0f, 0.0f));
			}
		}
		void DestroyMaterial() {
			if (_material != null) {
#if UNITY_EDITOR
				Material.DestroyImmediate(_material);
#else
				Material.Destroy(_material);
#endif
				_material = null;
			}
		}
		void SetupDesc(ref RenderTextureDescriptor desc, int w, int h) {
			desc.autoGenerateMips = false;
			desc.colorFormat = RenderTextureFormat.ARGB32;
			desc.depthBufferBits = 0;
			desc.dimension = TextureDimension.Tex2D;
			desc.enableRandomWrite = false;
			//_desc.flags = 
			desc.memoryless = RenderTextureMemoryless.None;
			desc.msaaSamples = 1;
			desc.shadowSamplingMode = ShadowSamplingMode.None;
			desc.sRGB = false;
			desc.useMipMap = false;
			desc.volumeDepth = 1;
			desc.vrUsage = VRTextureUsage.None;
			desc.width = w;
			desc.height = h;
		}
		RenderTexture AllocRT(int w, int h, FilterMode filterMode) {
			var desc = new RenderTextureDescriptor(0, 0);
			SetupDesc(ref desc, w, h);
			var rt = RenderTexture.GetTemporary(desc);
			rt.filterMode = filterMode;
			return rt;
		}
		void FreeRT(RenderTexture rt) {
			RenderTexture.ReleaseTemporary(rt);
		}
	}
}
