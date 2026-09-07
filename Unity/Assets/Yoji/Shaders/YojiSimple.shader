Shader "Yoji/Simple"
{
    Properties
    {
        _LineWidth ("LineWidth", Range(1.0, 10.0)) = 2.0
        [CosAngle] _SmoothAngle ("SmoothAngle", Range(0.0, 180.0)) = 0.5
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _BackLineDensity ("BackLineDensity", Range(0.0, 1.0)) = 0.05
        _EdgeLineDensity ("EdgeLineDensity", Range(0.0, 1.0)) = 1.0
        _FrontLineDensity ("FrontLineDensity", Range(0.0, 1.0)) = 1.0
        [Toggle(_PLANECLIP_ON)] _PlaneClip ("PlaneClip", Int) = 0
        [Toggle(_BREAK_ON)] _BreakOn ("Break", Int) = 0
        _BreakLevel ("BreakLevel", Float) = 0.0
        _BreakOrigin ("BreakOrigin", Vector) = (0, 0, 0, 0)
        _BreakRandom ("BreakRandom", Float) = 0.0
    }
    SubShader
    {
        Tags {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Cull Off
            Lighting Off
            BlendOp Add, Add
            Blend One One, One One
            ZWrite Off
            ZTest Always

        HLSLPROGRAM
            #pragma multi_compile _ COMPUTE_SHADER
            #pragma multi_compile _ _PLANECLIP_ON
            #pragma multi_compile _ _BREAK_ON
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            //#pragma debug
            #include "UnityCG.cginc"
            #define NO_OPTIMIZE
            //#define NO_CULL // 線分のカリングをしない(NO_OPTIMIZE時のみ有効)
            //#define COMPUTE_SHADER
            #include "Quat.cginc"
            #include "Random.cginc"

            static const uint NoCullShift = 0;
            static const uint NoSmoothAngleShift = 1;
            static const uint NoFrontShift = 2;
            static const uint NoCull = 1 << NoCullShift;
            static const uint NoSmoothAngle = 1 << NoSmoothAngleShift;
            static const uint NoFront = 1 << NoFrontShift;

            struct appdata
            {
                float3 beginPos : POSITION;
                float3 normal0 : NORMAL;
                float4 normal1 : TANGENT;
                half4 color : COLOR;
                float3 endPos : TEXCOORD0;
                // adjuster.x : horizontal offset
                // adjuster.y : vertial offset
                // adjuster.z : no smoothangle
                // adjuster.w : no cull
                float4 adjuster : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
            };

#ifdef COMPUTE_SHADER
            StructuredBuffer<float4x4> _TransformMatrix;
#endif
            float _LineWidth;
            float _BackLineDensity;
            float _EdgeLineDensity;
            float _FrontLineDensity;
            float _BreakLevel;
            float3 _BreakOrigin;
            float _BreakRandom;
            static const float ColorScale = 1.0 / 255.0;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _SmoothAngle)
                // _LineDensity.x : BackLineDensity
                // _LineDensity.y : EdgeLineDensity
                // _LineDensity.z : EdgeLineDensity
                // _LineDensity.w : FrontLineDensity
                //UNITY_DEFINE_INSTANCED_PROP(float4, _LineDensity)
                UNITY_DEFINE_INSTANCED_PROP(float3, _PlanePosition)
                UNITY_DEFINE_INSTANCED_PROP(float3, _PlaneNormal)
            UNITY_INSTANCING_BUFFER_END(Props)


            bool cut_by_plane(float3 planePoint, float3 planeNormal, inout float3 beginPos, inout float3 endPos)
            {
                float3 pa = beginPos - planePoint;
                float3 pb = endPos - planePoint;

                float dotPA = dot(pa, planeNormal);
                float dotPB = dot(pb, planeNormal);

                if (dotPA > 0.0 && dotPB < 0.0)
                {
                    float3 ab = endPos - beginPos;
                    float rate = abs(dotPA) / (abs(dotPA) + abs(dotPB));
                    endPos = beginPos + ab * rate;
                }
                else if (dotPA < 0.0 && dotPB > 0.0)
                {
                    float3 ba = beginPos - endPos;
                    float rate = abs(dotPB) / (abs(dotPB) + abs(dotPA));
                    beginPos = endPos + ba * rate;
                }
                else if (dotPA <= 0.0 && dotPB <= 0.0)
                {
                    return true;
                }
                return false;
            }

            void break_model(float3 localPos, float3 origin, inout float3 beginPos, inout float3 endPos, float level)
            {
                float3 center = (beginPos + endPos) * 0.5;
                float3 d0 = beginPos - center;
                float3 d1 = endPos - center;

                float3 axis = normalize(center - origin);

                float rnd = (LFSR_Rand_Gen((localPos.x + localPos.y + localPos.z) * 1000) & 0xff) * (1.0/255.0);
                level *= (1.0 + rnd * _BreakRandom);


                float4 q = quat_axis_angle(axis, level);
                //float4 q = quat_axis_angle(float3(0, 1, 0), level);
                d0 = quat_transform(q, d0);
                d1 = quat_transform(q, d1);

                center += (axis * level);
                beginPos = center + d0;
                endPos = center + d1;
            }

            // 指定の角度より広ければ1狭ければ0
            inline float calc_smooth_angle(float3 nml0, float3 nml1)
            {
                float d = dot(nml0, nml1);
                //return step(UNITY_ACCESS_INSTANCED_PROP(Props, _SmoothAngle), d);
                return UNITY_ACCESS_INSTANCED_PROP(Props, _SmoothAngle) >= d ? 0 : 1;
            }

#ifdef COMPUTE_SHADER
            v2f vert(appdata v, uint instanceID : SV_InstanceID)
#else
            v2f vert(appdata v)
#endif
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float lineWidth = _LineWidth;

#ifdef COMPUTE_SHADER
                float3 worldBegin = mul(_TransformMatrix[instanceID], float4(v.beginPos.xyz, 1)).xyz;
                float3 worldEnd = mul(_TransformMatrix[instanceID], float4(v.endPos.xyz, 1)).xyz;
                float3 worldNormal0 = normalize(mul(_TransformMatrix[instanceID], float4(v.normal0, 0)).xyz);
                float3 worldNormal1 = normalize(mul(_TransformMatrix[instanceID], float4(v.normal1, 0)).xyz);
#else
                float3 worldBegin = mul(unity_ObjectToWorld, float4(v.beginPos.xyz, 1.0)).xyz;
                float3 worldEnd = mul(unity_ObjectToWorld, float4(v.endPos.xyz, 1.0)).xyz;
                float3 worldNormal0 = UnityObjectToWorldNormal(v.normal0.xyz);
                float3 worldNormal1 = UnityObjectToWorldNormal(v.normal1.xyz);
#endif

#ifdef _BREAK_ON
#   ifdef COMPUTE_SHADER
                float3 breakOrigin = mul(_TransformMatrix[instanceID], float4(_BreakOrigin.xyz, 1.0)).xyz;
#   else
                float3 breakOrigin = mul(unity_ObjectToWorld, float4(_BreakOrigin.xyz, 1.0)).xyz;
#   endif
                break_model((v.beginPos.xyz + v.endPos.xyz) * 0.5, breakOrigin, worldBegin, worldEnd, _BreakLevel);
#endif

#ifdef _PLANECLIP_ON
                lineWidth *= cut_by_plane(UNITY_ACCESS_INSTANCED_PROP(Props, _PlanePosition), UNITY_ACCESS_INSTANCED_PROP(Props, _PlaneNormal), worldBegin, worldEnd)? 0.0 : 1.0;
#endif
                float4 clipBegin = UnityWorldToClipPos(worldBegin);
                float4 clipEnd = UnityWorldToClipPos(worldEnd);

                float3 centerPos = (worldBegin + worldEnd) * 0.5;
                float3 viewVec = UnityWorldSpaceViewDir(centerPos);
                //
                float4 outputPos = clipBegin;
                clipBegin.xyz *= (1.0/clipBegin.w);
                clipEnd.xyz *= (1.0/clipEnd.w);
                //
                float2 vert = normalize(clipEnd.xy - clipBegin.xy);
                float2 horz = float2(-vert.y, vert.x);
                float2 scale = float2(_ScreenParams.z - 1.0, _ScreenParams.w - 1.0);
                vert.xy *= scale.xy;
                horz.xy *= scale.xy;
                //
                outputPos.xyz *= (1.0/outputPos.w);

                float4 outputColor = (v.color * ColorScale) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float dot0 = dot(worldNormal0, viewVec);
                float dot1 = dot(worldNormal1, viewVec);

                uint renderFlag = (uint)v.adjuster.w;
                uint noCull = (renderFlag & NoCull) >> NoCullShift;
                uint noFront = (renderFlag & NoFront) >> NoFrontShift;
                uint noSmoothAngle = (renderFlag & NoSmoothAngle) >> NoSmoothAngleShift;
#ifdef NO_OPTIMIZE
#   ifdef NO_CULL
#   else
                float smooth_angle = (noSmoothAngle)? 1.0 : (1.0 - calc_smooth_angle(worldNormal0, worldNormal1));
                if (noCull)
                {
                    outputColor *= _EdgeLineDensity;
                    //lineWidth *= smooth_angle;
                }
                else
                {
                    // Back Line
                    if (dot0 < 0.0 && dot1 < 0.0)
                    {
                        outputColor *= _BackLineDensity;
                        lineWidth *= smooth_angle;
                    }
                    // Edge Line
                    if ((dot0 >= 0.0 && dot1 < 0.0) || (dot0 < 0.0 && dot1 >= 0.0))
                    {
                        outputColor *= _EdgeLineDensity;
                    }
                    // Front Line
                    if (!noFront && dot0 >= 0.0 && dot1 >= 0.0)
                    {
                        outputColor *= _FrontLineDensity;
                        lineWidth *= smooth_angle;
                    }
                }
#   endif
#else

                // 0b000 = back
                // 0b001 = edge
                // 0b010 = edge
                // 0b011 = front
                uint wireFlag = (uint)step(0.0, dot0) | ((uint)step(0.0, dot1) << 1);

                // noFrontは、frontを無効にして、edgeに置き換える
                // - noFrontが1で、wireFlagが2,3の時だけ、1引かれて1,2になる
                wireFlag = wireFlag - ((wireFlag >> 1) & noFront);

                // noCullは、back/frontを無効にして、edgeに置き換える
                // - noCullが1で、wireFlagが0,1の時だけ、1足されて1,2になる
                wireFlag = wireFlag + ((~wireFlag >> 1) & noCull);
                // - noCullが1で、wireFlagが2,3の時だけ、1引かれて1,2になる
                wireFlag = wireFlag - ((wireFlag >> 1) & noCull);

                //outputColor *= UNITY_ACCESS_INSTANCED_PROP(Props, _LineDensity)[flag & 0x03 | nocull | nocull << 1];
                half4 linedensity = half4(_BackLineDensity, _EdgeLineDensity, _EdgeLineDensity, _FrontLineDensity);
                outputColor *= linedensity[wireFlag & 0x03];

                const half4 apply = half4(1, 0, 0, 1);
                const half2 lineWidthScale = half2(0, 1);
                int smooth_angle = calc_smooth_angle(worldNormal0, worldNormal1);
                lineWidth *= 1.0 - (lineWidthScale[smooth_angle | noSmoothAngle] * apply[wireFlag & 0x03]);
#endif // NO_OPTIMIZE

                outputPos.xy += vert * lineWidth * v.adjuster.y;
                outputPos.xy += horz * lineWidth * v.adjuster.x;
                outputPos.xyz *= outputPos.w;

                o.vertex = outputPos;

                const float3 ntscScale = float3(0.298912, 0.586611, 0.114478);
                outputColor.a = dot(outputColor.rgb, ntscScale);
                o.color = outputColor;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 col = i.color;// * 0.5;
                return col;
            }
        ENDHLSL
        }
    }
    CustomEditor "Yoji.Editor.SimpleCustomInspector"
}
