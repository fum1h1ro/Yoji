Shader "Hidden/YojiBlurComposit" {
  Properties {
    _Tex0 ("Texture", 2D) = "white" {}
    _Tex1 ("Texture", 2D) = "white" {}
    _Tex2 ("Texture", 2D) = "white" {}
    _Tex3 ("Texture", 2D) = "white" {}
  }
  SubShader {
    // No culling or depth
    Cull Off ZWrite Off ZTest Always

    Pass {
      Blend Off
      //Blend One One

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ YOJI_BLUR_2 YOJI_BLUR_3 YOJI_BLUR_4
#include "UnityCG.cginc"

      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
      };

      sampler2D _Tex0;
      sampler2D _Tex1;
      sampler2D _Tex2;
      sampler2D _Tex3;

      v2f vert(appdata v) {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
      }
      fixed4 frag(v2f i) : SV_Target {
        half4 col0 = tex2D(_Tex0, i.uv);
#if defined(YOJI_BLUR_2) || defined(YOJI_BLUR_3) || defined(YOJI_BLUR_4)
        half4 col1 = tex2D(_Tex1, i.uv);
#endif
#if defined(YOJI_BLUR_3) || defined(YOJI_BLUR_4)
        half4 col2 = tex2D(_Tex2, i.uv);
#endif
#if defined(YOJI_BLUR_4)
        half4 col3 = tex2D(_Tex3, i.uv);
#endif

#ifdef YOJI_BLUR_2
        half4 r = (col0 + col1) * (1.0/2.0);
#elif YOJI_BLUR_3
        half4 r = (col0 + col1 + col2) * (1.0/3.0);
#elif YOJI_BLUR_4
        half4 r = (col0 + col1 + col2 + col3) * (1.0/4.0);
#else
        half4 r = col0;
#endif
        return r;
      }
ENDCG
    }
  }
}
