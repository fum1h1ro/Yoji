Shader "Hidden/Yoji/Bloom" {
  Properties {
    _MainTex ("Texture", 2D) = "white" {}
    //_BlendDst ("BlendMode", Int) = 0
  }

  CGINCLUDE
    #include "UnityCG.cginc"
    struct appdata {
      float4 vertex : POSITION;
      float2 uv : TEXCOORD0;
    };

    struct v2f {
      float4 vertex : SV_POSITION;
      half2 uv : TEXCOORD0;
    };

    struct v2f_gauss {
      float4 vertex : SV_POSITION;
      half2 uv : TEXCOORD0;
      half2 offs : TEXCOORD1;
    };

    sampler2D _MainTex;
    half4 _MainTex_TexelSize;

    v2f vert(appdata v) {
      v2f o;
      o.vertex = UnityObjectToClipPos(v.vertex);
      o.uv = v.uv;
      return o;
    }
    v2f_gauss vertV(appdata v) {
      v2f_gauss o;
      o.vertex = UnityObjectToClipPos(v.vertex);
      o.uv = v.uv;
      o.offs = _MainTex_TexelSize * half2(0.0, 1.0) * 1.7;
      return o;
    }
    v2f_gauss vertH(appdata v) {
      v2f_gauss o;
      o.vertex = UnityObjectToClipPos(v.vertex);
      o.uv = v.uv;
      o.offs = _MainTex_TexelSize * half2(1.0, 0.0) * 1.7;
      return o;
    }
    // 画素にかける係数 ガウス関数から算出
    static const half curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };  // gauss'ish blur weights
    half4 fragGauss(v2f_gauss i) : SV_Target {
      half2 filterWidth = i.offs;
#if UNITY_HALF_TEXEL_OFFSET
      half2 coords = i.uv - filterWidth * 3.0 - i.offs * 0.5;
#else
      half2 coords = i.uv - filterWidth * 3.0;
#endif
      half4 col = 0;
      for (int i = 0; i < 7; ++i) {
        half4 tap = tex2D(_MainTex, coords);
        col += tap * 1.0 * (curve[i] * (1.0 + tap.a * 0.5));
        coords += filterWidth;
      }
      return col;
    }
  ENDCG

  SubShader {
    // No culling or depth
    Cull Off ZWrite Off ZTest Always

    Pass {
      Name "0"
      Blend SrcAlpha One
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        half4 frag (v2f i) : SV_Target {
          half4 col = tex2D(_MainTex, i.uv);
          // just invert the colors
          //col = 1 - col;
          col.a = 0.75f;
          return col;
        }
      ENDCG
    }
    Pass {
      Name "1 Blend"
      Blend SrcAlpha [_BlendDst]
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        float _Scale;
        float _BlendAlpha;
        half4 frag (v2f i) : SV_Target {
          half4 col = tex2D(_MainTex, i.uv);
          col.rgb *= _Scale;
          col.a = _BlendAlpha;
          return col;
        }
      ENDCG
    }
    Pass {
      Name "2 Gauss V"
      Blend One Zero
      CGPROGRAM
        #pragma vertex vertV
        #pragma fragment fragGauss
        #include "UnityCG.cginc"
      ENDCG
    }
    Pass {
      Name "3 Gauss H"
      Blend One Zero
      CGPROGRAM
        #pragma vertex vertH
        #pragma fragment fragGauss
        #include "UnityCG.cginc"
      ENDCG
    }
    Pass {
      Name "4 First Cut out"
      Blend SrcAlpha Zero
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        half4 frag(v2f i) : SV_Target {
          half4 col = tex2D(_MainTex, i.uv);
          //half maxval = max(max(col.x, col.y), col.z);
          //maxval = max(maxval - 0.9, 0);
          //half scale = min(maxval * 1000000.0, 1);
          //col.a = scale;
          return col;
        }
      ENDCG
    }
    Pass {
      Name "5 "
      Blend SrcAlpha OneMinusSrcAlpha
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        half4 frag (v2f i) : SV_Target {
          half4 col = tex2D(_MainTex, i.uv);
          col *= 0.65;//1.0/6.0;
          return col;
        }
      ENDCG
    }
    Pass {
      Name "6 Scale and Overwrite"
      Blend One Zero
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        float _Scale;
        // https://beesbuzz.biz/code/16-hsv-color-transforms
        inline float3 shift_col(float3 RGB, float3 shift) {
          float3 RESULT = float3(RGB);
          float VSU = shift.z*shift.y*cos(shift.x*3.14159265/180);
          float VSW = shift.z*shift.y*sin(shift.x*3.14159265/180);

          RESULT.x = (.299*shift.z+.701*VSU+.168*VSW)*RGB.x
            + (.587*shift.z-.587*VSU+.330*VSW)*RGB.y
            + (.114*shift.z-.114*VSU-.497*VSW)*RGB.z;

          RESULT.y = (.299*shift.z-.299*VSU-.328*VSW)*RGB.x
            + (.587*shift.z+.413*VSU+.035*VSW)*RGB.y
            + (.114*shift.z-.114*VSU+.292*VSW)*RGB.z;

          RESULT.z = (.299*shift.z-.3*VSU+1.25*VSW)*RGB.x
            + (.587*shift.z-.588*VSU-1.05*VSW)*RGB.y
            + (.114*shift.z+.886*VSU-.203*VSW)*RGB.z;

          return (RESULT);
        }
        half4 frag (v2f i) : SV_Target {
          half4 col = tex2D(_MainTex, i.uv);
          col.rgb = shift_col(col.rgb * _Scale, float3(0, 1.0 - col.a * _Scale * (1.0/4.0) * 2.0, 1));
          //col.rgb *= _Scale;
          //return pow(col, 1.0/2.2);
          return pow(col, 2.2);
          //return col;
        }
      ENDCG
    }
    Pass {
      Name "7 GammaBlend"
      Blend SrcAlpha [_BlendDst]
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        float _Scale;
        float _BlendAlpha;
        half4 frag (v2f i) : SV_Target {
          half4 col = tex2D(_MainTex, i.uv);
          col.rgb = pow(col.rgb * _Scale, 1.0/2.2);
          col.a = _BlendAlpha;
          return col;
        }
      ENDCG
    }
  }
}
