using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

#if false
namespace Yoji {
	[ExecuteInEditMode]
	public class YojiPipelineAsset : RenderPipelineAsset {
#if UNITY_EDITOR
		[UnityEditor.MenuItem("Yoji/CreatePipelineAsset")]
		static void CreatePipelineAsset() {
			var instance = ScriptableObject.CreateInstance<YojiPipelineAsset>();
			UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/YojiRenderPipeline.asset");
		}
#endif

		protected override IRenderPipeline InternalCreatePipeline() {
			return new YojiPipelineInstance();
		}
	}

	public class YojiPipelineInstance : RenderPipeline {
		public YojiPipelineInstance() {
		}

		public override void Render(ScriptableRenderContext renderContext, Camera[] cameras) {
			base.Render(renderContext, cameras);

			UnityEngine.Profiling.Profiler.BeginSample("HOGE");
			var cmd = new CommandBuffer();
			cmd.ClearRenderTarget(true, true, Color.black);
			renderContext.ExecuteCommandBuffer(cmd);
			cmd.Release();
			renderContext.Submit();
			UnityEngine.Profiling.Profiler.EndSample();









		}
	}
}
#endif

