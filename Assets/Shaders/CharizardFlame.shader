Shader "Custom/CharizardFlame"
{
    Properties
    {
        _MainColor ("Base Color", Color) = (1,0.5,0,1) // Red/Orange at the flame's base
        _TipColor ("Tip Color", Color) = (1,1,0,1)     // Yellow/White at the flame's tips
        _MaxDisplacement ("Max Displacement", Range(0, 1)) = 0.05 // Maximum outward displacement of vertices
        _DisplacementSpeed ("Displacement Speed", Float) = 1.0   // Speed of the vertex displacement animation (overall flame movement)
        _NoiseScale ("Noise Scale", Float) = 1.0       // Base scale/frequency of the noise applied to displacement
        _FlickerSpeed ("Flicker Speed", Float) = 2.0   // Speed of the overall flame flicker (intensity)
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.5 // How much the flicker affects displacement
        _AlphaFadeOut ("Alpha Fade Out", Range(0, 1)) = 0.5 // Controls how quickly transparency fades at edges
        _DistortionMagnitude ("Distortion Magnitude", Range(0, 0.5)) = 0.1 // Additional noise for subtle color distortion
        _DistortionSpeed ("Distortion Speed", Float) = 0.5 // Speed of color distortion
        _Spikiness ("Spikiness", Range(1.0, 10.0)) = 4.0 // Controls how sharp and pointy the flame tips are (increased default)

        // New properties for wind and fBm control
        _WindDirection ("Wind Direction (X,Y,Z)", Vector) = (0.5, 0.0, 0.0) // Direction of the wind (e.g., (1,0,0) for right)
        _WindSpeed ("Wind Speed", Float) = 0.5 // How fast the flame distorts with wind
        _Octaves ("Noise Octaves", Int) = 4 // Number of noise layers for fractal detail
        _Lacunarity ("Noise Lacunarity", Float) = 2.0 // How much frequency increases per octave
        _Persistence ("Noise Persistence", Float) = 0.5 // How much amplitude decreases per octave
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            half4 _MainColor;
            half4 _TipColor;
            float _MaxDisplacement;
            float _DisplacementSpeed;
            float _NoiseScale;
            float _FlickerSpeed;
            float _FlickerIntensity;
            float _AlphaFadeOut;
            float _DistortionMagnitude;
            float _DistortionSpeed;
            float _Spikiness;

            // New properties
            float3 _WindDirection;
            float _WindSpeed;
            int _Octaves;
            float _Lacunarity;
            float _Persistence;


            // --- Noise Functions ---
            // A simple 3D hash-based noise.
            float hash(float n) { return frac(sin(n) * 43758.5453123); }

            float noise(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f); // Smooth interpolation (cubic)

                float n = p.x + p.y * 157.0 + p.z * 113.0;
                float res = lerp(lerp(lerp(hash(n + 0.0), hash(n + 1.0), f.x),
                                     lerp(hash(n + 157.0), hash(n + 158.0), f.x), f.y),
                                lerp(lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                                     lerp(hash(n + 113.0 + 157.0), hash(n + 114.0 + 157.0), f.x), f.y), f.z);
                return res * 2.0 - 1.0; // Remap to -1 to 1 range
            }

            // Fractal Brownian Motion (fBm) function for layered noise detail
            // Creates more complex, natural-looking noise with multiple frequencies
            float fbm(float3 coord, float timeOffset, int octaves, float lacunarity, float persistence)
            {
                float total = 0.0;
                float frequency = 1.0;
                float amplitude = 1.0;
                // Loop through specified octaves
                for (int i = 0; i < octaves; i++)
                {
                    // Add noise scaled by amplitude, at current frequency and time offset
                    total += noise(coord * frequency + timeOffset) * amplitude;
                    frequency *= lacunarity;  // Increase frequency for next octave
                    amplitude *= persistence; // Decrease amplitude for next octave
                }
                return total;
            }


            // --- Vertex Input/Output Structures ---
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float  displacementMagnitude : TEXCOORD2;
                float3 objectPos : TEXCOORD3;
            };

            // --- Vertex Shader ---
            v2f vert (appdata v)
            {
                v2f o;

                o.objectPos = v.vertex.xyz;

                float time = _Time.y * _DisplacementSpeed;

                // Add a wind offset to the noise sampling coordinates
                // This makes the noise "flow" or "distort" over time in a given direction
                float3 windOffset = normalize(_WindDirection) * _Time.y * _WindSpeed;
                float3 noiseSampleCoord = o.objectPos * _NoiseScale + windOffset;

                // Use fBm for combined noise, creating more detailed and consistent spikes
                float combinedNoise = fbm(noiseSampleCoord, time, _Octaves, _Lacunarity, _Persistence);

                // Remap combined noise from [-1, 1] to [0, 1] for outward displacement only
                combinedNoise = combinedNoise * 0.5 + 0.5;
                // Apply a power function to emphasize peaks, making the displacement spikier
                // _Spikiness controls the sharpness of the flame tips
                combinedNoise = pow(combinedNoise, _Spikiness);

                // Apply a global flicker intensity based on time
                float flickerFactor = sin(_Time.y * _FlickerSpeed) * 0.5 + 0.5;
                float finalDisplacement = combinedNoise * _MaxDisplacement * (1.0 + _FlickerIntensity * flickerFactor);

                // Apply displacement along the vertex normal in object space
                v.vertex.xyz += v.normal * finalDisplacement;

                o.displacementMagnitude = finalDisplacement;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;

                return o;
            }

            // --- Fragment Shader ---
            half4 frag (v2f i) : SV_Target
            {
                half colorBlendFactor = i.uv.y;
                half4 finalColor = lerp(_MainColor, _TipColor, colorBlendFactor);

                float time = _Time.y;

                // Noise for chaotic alpha flickering and distortion
                // Also apply wind offset for alpha noise consistency with displacement
                float3 alphaNoiseCoord = i.objectPos * _NoiseScale * 1.5 + _WindDirection * _Time.y * _WindSpeed + time * _DistortionSpeed;
                float alphaNoise = noise(alphaNoiseCoord);
                alphaNoise = saturate(alphaNoise * 0.5 + 0.5);

                // Additional noise for subtle color distortion/movement within the flame
                float3 distortionNoiseCoord = i.objectPos * _NoiseScale * 3.0 + _WindDirection * _Time.y * _WindSpeed * 0.5 + time * _DistortionSpeed;
                float distortionNoise = noise(distortionNoiseCoord);
                finalColor.rgb += distortionNoise * _DistortionMagnitude;
                finalColor.rgb = saturate(finalColor.rgb);

                float alpha = 1.0 - saturate((i.displacementMagnitude / _MaxDisplacement) * _AlphaFadeOut + (1.0 - alphaNoise));
                alpha = saturate(alpha);

                finalColor.a = alpha;

                return finalColor;
            }
            ENDCG
        }
    }
}
