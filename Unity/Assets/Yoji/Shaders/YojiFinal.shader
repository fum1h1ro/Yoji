// SRPの最終パス。HDRシーンRT（Linear Rec.709、RGBA16F想定）をバックバッファへ書く。
// フルスクリーン三角形をDrawProceduralで描く前提（メッシュ・頂点バッファ不要）:
//   cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3);
//
// SDR出力: _Kneeまでは恒等、それ以上はチャンネルごとに1へ漸近する肩（Reinhard風）。Linear値を返し、
//          sRGBバックバッファへの書き込み時にOSがエンコードする。
// HDR出力: HDROutputUtils.ConfigureHDROutput()がHDR_COLORSPACE_CONVERSION_AND_ENCODINGを立てた場合、
//          Rec709→出力色域への回転 → BT.2390（輝度のみ）でレンジ圧縮 → 出力エンコーディングのOETFをかける。
//          automaticHDRTonemappingと二重にならないよう、このパスを使うときはfalseにすること。
Shader "Hidden/Yoji/Final"
{
    Properties
    {
        _Exposure ("Exposure", Float) = 1.0
        _Knee ("SDR Knee", Range(0.0, 1.0)) = 0.8
        _PaperWhite ("Paper White (nits)", Float) = 203.0
        _MinNits ("Min Nits", Float) = 0.0
        _MaxNits ("Max Nits", Float) = 1000.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
        HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ HDR_COLORSPACE_CONVERSION_AND_ENCODING
            #pragma target 3.5

            // UnityCG.cginc とシンボルが衝突するため、こちらではCore側のみをincludeする
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"

            // XRは使わないプロジェクトなのでTEXTURE2D_Xではなく素のTEXTURE2Dを使う
            TEXTURE2D(_SceneColor);
            SAMPLER(sampler_SceneColor);

            float _Exposure;
            float _Knee;
            float _PaperWhite;
            float _MinNits;
            float _MaxNits;

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f Vert(uint vertexID : SV_VertexID)
            {
                v2f o;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }

            half4 Frag(v2f i) : SV_Target
            {
                float3 sceneColor = SAMPLE_TEXTURE2D(_SceneColor, sampler_SceneColor, i.uv).rgb;

                // 負値・NaN・Infのガード。65000はhalfの上限(65504)に収まる安全マージン
                float3 c = min(max(sceneColor * _Exposure, 0.0), 65000.0);

#ifdef HDR_COLORSPACE_CONVERSION_AND_ENCODING
                // BT.2390は10000nitsまでしか定義されていない。paperWhite乗算後にそれを超えないようクランプする
                c = min(c, 10000.0 / _PaperWhite);
                c = HDRMappingFromRec709(c, _PaperWhite, _MinNits, _MaxNits, HDRRANGEREDUCTION_BT2390, 0.0);
#else
                // SDR: _Kneeまでは恒等、その上はチャンネルごとに1へ漸近させる（明るい色は白へ寄る）
                float3 over = max(c - _Knee, 0.0);
                float range = 1.0 - _Knee;
                c = min(c, _Knee) + range * over / (over + range);
#endif
                return half4(c, 1.0);
            }
        ENDHLSL
        }
    }
}
