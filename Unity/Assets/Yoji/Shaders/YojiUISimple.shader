Shader "Yoji/UISimple"
{
    Properties
    {
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass {
            Cull Off
            Lighting Off
            BlendOp Add, Add
            Blend One One, One One
            ZWrite Off
            ZTest Always
        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma debug
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            //#define NO_OPTIMIZE

            struct appdata
            {
                float4 beginPos : POSITION;
                fixed4 color : COLOR0;
                float2 endPos : TEXCOORD0;
                // adjuster.x : 
                // adjuster.y : 
                float2 adjuster : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR0;
            };

            float _LineWidth;
            // _Color.rgb : Color
            // _Color.a : SmoothAngle
            float4 _Color;


            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float lineWidth = 16.0;//_LineWidth;
                float4 bgn = UnityObjectToClipPos(v.beginPos);
                float4 end = UnityObjectToClipPos(float4(v.endPos, 0, 1));
                //
                float4 pos = bgn;
                bgn.xyz *= (1.0/bgn.w);
                end.xyz *= (1.0/end.w);
                //
                float2 vert = normalize(end.xy - bgn.xy);
                float2 horz = float2(-vert.y, vert.x);
                float2 scale = float2(_ScreenParams.z - 1.0, _ScreenParams.w - 1.0);
                vert.xy *= scale.xy;
                horz.xy *= scale.xy;
                //
                pos.xyz *= (1.0/pos.w);
                //pos.xy += vert * lineWidth * (v.adjuster.y * 1);
                //pos.xy += horz * lineWidth * (v.adjuster.x * 1);
                pos.xy += vert * lineWidth * (-0.5 * 1);
                pos.xy += horz * lineWidth * v.adjuster.x;
                pos.xyz *= pos.w;

                o.vertex = pos;

                const float3 ntscScale = float3(0.298912, 0.586611, 0.114478);
                float4 outputColor = v.color;
                outputColor.a = dot(outputColor.rgb, ntscScale);
                outputColor.a = 1;
                //outputColor.xy = v.adjuster.xy;
                o.color = outputColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = i.color;
                return col;
            }
        ENDHLSL
        }
    }
}
