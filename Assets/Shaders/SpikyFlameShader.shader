Shader "Custom/SpikyFlameDistortion"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,0.5,0,1) // Core flame color (orange/red)
        _PeakColor ("Peak Color", Color) = (1,1,0,1)   // Peak flame color (yellow/white)
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 5.0 // Overall glow intensity
        _DisplacementStrength ("Displacement Strength", Range(0, 1)) = 0.15 // How much to displace
        _UpwardDisplacementInfluence ("Upward Displacement Influence", Range(0, 1)) = 0.5 // How much to push upwards
        _NoiseFrequency ("Noise Frequency", Range(1, 20)) = 10.0 // Overall scale of the noise
        _NoiseSpeed ("Noise Speed", Range(0, 5)) = 1.0 // How fast the noise scrolls
        _NoisePower ("Noise Power (Spikiness)", Range(1, 10)) = 3.0 // Exaggerates peaks for spikiness
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.4 // Cuts off transparent parts sharply
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" } // Render as transparent
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending (can change to Additive for glow)
        Cull Off // Render both sides of the mesh (useful for volume)

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
                float2 uv : TEXCOORD0; // UVs are still important for potential texture/noise mapping
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                float4 flameColor : COLOR; // Color & Alpha calculated in vertex shader
            };

            fixed4 _BaseColor;
            fixed4 _PeakColor;
            float _EmissionStrength;
            float _DisplacementStrength;
            float _UpwardDisplacementInfluence;
            float _NoiseFrequency;
            float _NoiseSpeed;
            float _NoisePower;
            float _AlphaThreshold;

            // Simplified "Fractal Brownian Motion" (FBM) using multiple sine waves
            // In a real production shader, you'd sample a noise texture or implement
            // a proper procedural noise function (like Perlin or Simplex noise).
            float GetFractalNoise(float3 pos, float time)
            {
                float noiseVal = 0.0;
                float amplitude = 1.0;
                float frequency = _NoiseFrequency; // Base frequency from property

                // Layer 1
                noiseVal += sin(pos.y * frequency + time * _NoiseSpeed) * amplitude;
                noiseVal += cos(pos.x * frequency * 0.7 + time * _NoiseSpeed * 0.8) * amplitude;

                // Layer 2 (higher frequency, lower amplitude)
                amplitude *= 0.5;
                frequency *= 2.0;
                noiseVal += sin(pos.y * frequency + time * _NoiseSpeed * 1.5) * amplitude;
                noiseVal += cos(pos.x * frequency * 0.9 + time * _NoiseSpeed * 1.3) * amplitude;

                // Layer 3 (even higher frequency, even lower amplitude)
                amplitude *= 0.5;
                frequency *= 2.0;
                noiseVal += sin(pos.y * frequency * 1.2 + time * _NoiseSpeed * 2.0) * amplitude;
                noiseVal += cos(pos.x * frequency * 0.6 + time * _NoiseSpeed * 1.7) * amplitude;

                // Normalize to 0-1 range (roughly)
                noiseVal = noiseVal * 0.25 + 0.5; // Adjust based on how many layers are added

                // Apply power to exaggerate peaks (spikiness)
                noiseVal = pow(noiseVal, _NoisePower);

                return saturate(noiseVal); // Clamp between 0 and 1
            }

            v2f vert (appdata v)
            {
                v2f o;

                // Transform vertex position to world space for noise calculation
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Get the noise value
                float distortionFactor = GetFractalNoise(worldPosition, _Time.y);

                // Calculate displacement direction
                // Combine displacement along normal and upward (local Y) direction
                float3 displacementDirection = normalize(
                    v.normal * (1.0 - _UpwardDisplacementInfluence) +
                    float3(0,1,0) * _UpwardDisplacementInfluence // Local Y-axis is up
                );

                // Apply displacement
                v.vertex.xyz += displacementDirection * _DisplacementStrength * distortionFactor;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Pass UVs for potential future use (e.g. texture)
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // Recalculate world position after displacement
                UNITY_TRANSFER_FOG(o,o.vertex);

                // Flame color based on distortion factor and vertex height
                // Lerp between base color and peak color
                fixed3 interpolatedColor = lerp(_BaseColor.rgb, _PeakColor.rgb, distortionFactor);
                
                // Add a slight vertical gradient to blend colors from bottom to top
                // Normalize vertex height (from 0 at base to 1 at max height of the flame)
                // Assuming your flame base is at Y=0 in object space, and it extends upwards.
                float normalizedHeight = saturate(v.vertex.y / (max(v.vertex.y, 0.001) + 1.0)); // Adjust this based on your actual mesh height
                interpolatedColor = lerp(interpolatedColor, _PeakColor.rgb, normalizedHeight * 0.5); // Make top brighter

                o.flameColor.rgb = interpolatedColor;
                o.flameColor.a = distortionFactor; // Alpha based on noise (will be clipped in frag)

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Crucial for spiky look: Alpha clipping to sharply cut off transparent areas
                clip(i.flameColor.a - _AlphaThreshold);

                fixed4 finalColor = fixed4(i.flameColor.rgb, i.flameColor.a);

                // Add emission based on the calculated flame color and emission strength
                finalColor.rgb += finalColor.rgb * _EmissionStrength; 

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Standard"
}