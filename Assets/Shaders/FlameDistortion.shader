// Unity Shader basic structure
Shader "Custom/FlameDistortion"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1) // Base color
        _EmissionColor ("Emission Color", Color) = (1,0.5,0,1) // Emission color
        _DisplacementStrength ("Displacement Strength", Range(0, 1)) = 0.1 // How much to displace
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0 // How fast the basic distortion pulsates
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.5 // For alpha clipping
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending
        Cull Off // Render both sides of the mesh (optional, can be useful for flames)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1; // World position for effects
                UNITY_FOG_COORDS(2)
                float4 color : COLOR; // For passing vertex color information to fragment
            };

            fixed4 _Color;
            fixed4 _EmissionColor;
            float _DisplacementStrength;
            float _PulseSpeed;
            float _AlphaThreshold;

            // Simple "noise" function using sine/cosine for demonstration
            // In a real shader, this would be a noise texture lookup or a procedural noise function
            float GetDistortionValue(float3 worldPos, float time)
            {
                // Simple oscillating value based on time and world position
                // Combine multiple sin waves for more complexity
                float val = sin(worldPos.y * 5.0 + time * _PulseSpeed) * 0.5 + 0.5; // Upward pulse
                val += cos(worldPos.x * 3.0 + time * _PulseSpeed * 0.7) * 0.3 + 0.5; // Sideways pulse
                val *= 0.5; // Scale down for a better range
                return val;
            }

            v2f vert (appdata v)
            {
                v2f o;

                // Calculate distortion value
                float distortion = GetDistortionValue(mul(unity_ObjectToWorld, v.vertex).xyz, _Time.y);

                // Displace vertex along its normal
                // We multiply by distortion strength and the calculated distortion value
                float3 displacedNormal = v.normal * _DisplacementStrength * distortion;
                v.vertex.xyz += displacedNormal;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);

                // Simple color based on distortion value (making hotter colors at higher distortion)
                // In a real flame, you'd use a gradient texture or more complex blending
                o.color.rgb = lerp(_Color.rgb, fixed3(1, 0.5, 0), distortion * 2.0); // Orange to yellow/white
                o.color.a = distortion; // Alpha based on distortion

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Apply alpha clipping
                clip(i.color.a - _AlphaThreshold);

                fixed4 finalColor = fixed4(i.color.rgb, i.color.a);

                // Add emission
                finalColor.rgb += _EmissionColor.rgb * i.color.a; // Emission based on alpha

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Standard"
}