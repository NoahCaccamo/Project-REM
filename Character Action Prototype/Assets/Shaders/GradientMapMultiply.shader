// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Unlit alpha-blended shader.
// - no lighting
// - no lightmap support
// - no per-material color

// look at this for some changes: https://youtu.be/fx7ryOc15Uk?si=uoEOLs470UB-cChp&t=304

Shader "Testing/Gradient Map Multiply"
{
    Properties
    {
        _MainTex ("Base (RGBA)", 2D) = "white" {}
        _Color1("Hi Color", color) = (1.0,1.0,1.0,1.0)
        _Step1("Color Cutoff", range(0.0,2.0)) = 0.5
        _Color2("Lo Color", color) = (1.0,0.95,0.8,1.0)
        // _BackfaceTint("Backface Tint",range(0.0,1.0)) = 0
        _AlphaCutoff("Alpha Cutoff", range(0.0,1.0)) = 0.0
        _AlphaInfluence("Alpha Influence", range(0.0,50.0)) = 1.0

        _Intensity("Intensity", range(-1,10)) = 1

        _MKGlowPower("Emission Power", range(0,10)) = 0

        _MKGlowColor("Emission Color", color) = (1.0,0.95,0.8, 1.0)
        _Mask("Mask", 2D) = "white" {}
        _MaskCutoff("Mask Cutoff", range(-0.1,1)) = 0
        _Fog("Fog", range(0.0,1)) = 0
    }
    SubShader
    {
        //Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderType" = "MKGlow" }
        LOD 100

        //Lighting On
        Cull Back
        ZWrite Off
        //ColorMask 0
        
            ZTest LEqual
        Blend One OneMinusSrcAlpha//SrcAlpha OneMinusSrcAlpha
            //AlphatoMask On

        Pass
        {
            Cull Back
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata_t {
                    float4 vertex: POSITION;
                    float2 texcoord: TEXCOORD0;
                    fixed4 color: COLOR;
                    float2 maskcoord: TEXCOORD1;
                };

                struct v2f
                {
                    float4 vertex: SV_POSITION;
                    half2 texcoord: TEXCOORD0;
                    fixed4 color: COLOR;
                    half2 maskcoord: TEXCOORD1;
                };

                sampler2D _MainTex; //commented out????

                float4 _MainTex_ST;
                float4 _Color1;
                float4 _Color2;
                float _Step1;
                float _AlphaCutoff;
                float _AlphaInfluence;

                float _Intensity;

                sampler2D _Mask;
                float4 _Mask_ST;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.maskcoord = TRANSFORM_TEX(v.maskcoord, _Mask);
                o.color = v.color;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed2 myUVs = i.texcoord;
                myUVs.y = clamp(myUVs.y, 0, 1);
                fixed4 col = tex2D(_MainTex, myUVs);
                fixed4 mask = tex2D(_Mask, i.maskcoord);

                float blendV = col.r;
                float texA = col.a;

                _Step1 = lerp(_AlphaCutoff,2.0, _Step1);
                col = lerp(_Color2,_Color1,smoothstep(_AlphaCutoff,_Step1,blendV));
                //col.xyz *= _Intensity;
                col *= _Intensity;

                if(texA <= _AlphaCutoff) discard;
                texA = lerp(1.0,texA,_AlphaInfluence);

                col.xyz *= texA;
                col.a *= texA;
                col *= i.color;

                col *= mask.a;
                return fixed4(col.r, col.g, col.b, col.a);
            }
            ENDCG
        }

            Pass
            {
                Cull Front
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata_t {
                    float4 vertex : POSITION;
                    float2 texcoord : TEXCOORD0;
                    fixed4 color : COLOR;
                    float2 maskcoord : TEXCOORD1;
                    };

                struct v2f {
                    float4 vertex : SV_POSITION;
                    half2 texcoord : TEXCOORD0;
                    fixed4 color : COLOR;
                    half2 maskcoord : TEXCOORD1;
                    };

                sampler2D _MainTex;

                float4 _MainTex_ST;
                float4 _Color1;
                float4 _Color2;
                float _Step1;
                float _AlphaCutoff;
                float _AlphaInfluence;

                float _Intensity;

                sampler2D _Mask;

                float4 _Mask_ST;

                v2f vert(appdata_t v) {
                    v2f o;

                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                    o.maskcoord = TRANSFORM_TEX(v.maskcoord, _Mask);
                    o.color = v.color;
                    return o;
                    }

                fixed4 frag(v2f i) : SV_Target
                {
                    fixed2 myUVs = i.texcoord;
                    myUVs.y = clamp(myUVs.y, 0, 1);
                    fixed4 col = tex2D(_MainTex, myUVs);
                    fixed4 mask = tex2D(_Mask, i.maskcoord);
                    float blendV = col.r;
                    float texA = col.a;

                    _Step1 = lerp(_AlphaCutoff,2.0, _Step1);
                    col = lerp(_Color2,_Color1,smoothstep(_AlphaCutoff,_Step1,blendV));

                    col *= _Intensity;

                    if (texA <= _AlphaCutoff) discard;
                    texA = lerp(1.0,texA,_AlphaInfluence);
                    col.xyz *= texA;
                    col.a *= texA;
                    col *= i.color;
                    col *= mask.a;
                    return fixed4(col.r, col.g, col.b, col.a);
                }
                ENDCG
            }
    }
}
