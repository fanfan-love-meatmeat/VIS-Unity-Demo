Shader "Custom/depth_shader_v8_sim2real_fix"
{
    Properties {
        // ========================================================
        // 1. Fixed visualization range
        // ========================================================
        [Header(Absolute Visual Standard)]
        _VisualRangeMax ("Visual Standard (Fixed)", Float) = 10.0
        _MinDistance ("Min Start (Meters)", Float) = 0.0 
        
        [KeywordEnum(Rainbow, Grayscale)] _ColorMapMode ("Colormap Mode", Int) = 1
        _NearColor ("Near Color", Color) = (0,0,0,1)
        _FarColor ("Far Color", Color) = (1,1,1,1)

        // ========================================================
        // 2. Dynamic clipping
        // ========================================================
        [Header(Dynamic Clipping)]
        [Space(5)]
        _ClipThreshold ("Clip Threshold (Meters)", Range(0.0, 10.0)) = 5.0

        // ========================================================
        // 3. Sim-to-real: speckle dropout (random invalid pixels)
        // ========================================================
        [Header(Speckle Dropout)]
        [Space(5)]
        [Toggle(_ENABLE_SPECKLE)] _EnableSpeckle ("Enable Speckle", Float) = 1
        _SpeckleProb ("Dropout Probability", Range(0.0, 1.0)) = 0.01
        _NoisePixelSize ("Noise Pixel Size", Range(0.0, 20.0)) = 2.0
        [Toggle(_ENABLE_DYNAMIC_NOISE)] _EnableDynamicNoise ("Animated Noise", Float) = 1

        // ========================================================
        // 4. Sim-to-real: depth quantization
        // ========================================================
        [Header(Quantization)]
        [Space(5)]
        [Toggle(_ENABLE_QUANTIZATION)] _EnableQuantization ("Enable Quantization", Float) = 1
        _QuantizationSteps ("Quantization Steps", Range(0.0, 255.0)) = 40.0

        // ========================================================
        // 5. Sim-to-real: edge dropout
        // ========================================================
        [Header(Edge Dropout)]
        [Space(5)]
        [Toggle(_ENABLE_EDGE)] _EnableEdge ("Enable Edge", Float) = 1
        _EdgeThreshold ("Edge Threshold (Sensitivity)", Range(0.0, 0.2)) = 0.05
        // Control edge-dropout density: lower values produce sparse dots; higher values approach continuous edges.
        _EdgeNoiseProb ("Edge Noise Probability", Range(0.0, 1.0)) = 0.4
    }

    SubShader {
        Tags { "RenderType"="Opaque" }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _COLORMAPMODE_RAINBOW _COLORMAPMODE_GRAYSCALE
            #pragma shader_feature _ENABLE_SPECKLE
            #pragma shader_feature _ENABLE_DYNAMIC_NOISE
            #pragma shader_feature _ENABLE_QUANTIZATION
            #pragma shader_feature _ENABLE_EDGE

            #include "UnityCG.cginc"

            // Shader variables
            float _VisualRangeMax;
            float _MinDistance;
            fixed4 _NearColor, _FarColor;
            float _ClipThreshold;
            
            float _SpeckleProb;
            float _NoisePixelSize;
            float _QuantizationSteps;
            
            float _EdgeThreshold;
            float _EdgeNoiseProb; // Edge-dropout probability.
            
            sampler2D _CameraDepthTexture;

            struct v2f {
                float4 pos : SV_POSITION;
                float4 scrPos : TEXCOORD0;
            };

            // Pseudorandom function
            float rand(float2 seed) {
                return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
            }

            half3 HSVtoRGB(float H, float S, float V) {
                float C = V * S;
                float X = C * (1.0 - abs(fmod(H / 60.0, 2.0) - 1.0));
                float m = V - C;
                float3 RGB = float3(0, 0, 0);
                if (H >= 0.0 && H < 60.0) RGB = float3(C, X, 0);
                else if (H < 120.0) RGB = float3(X, C, 0);
                else if (H < 180.0) RGB = float3(0, C, X);
                else if (H < 240.0) RGB = float3(0, X, C);
                else if (H < 300.0) RGB = float3(X, 0, C);
                else RGB = float3(C, 0, X);
                return (RGB + m);
            }

            v2f vert(appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.scrPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // Read UV coordinates and raw depth.
                float2 uv = i.scrPos.xy / i.scrPos.w;
                float rawDepth01 = Linear01Depth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.scrPos)).r);
                
                // Unquantized depth in meters, used for edge detection.
                float trueDepthMeters = rawDepth01 * _ProjectionParams.z;

                // ========================================================
                // 1. Detect edges before quantization, using trueDepthMeters.
                // ========================================================
                bool isEdgeNoise = false;
                #if defined(_ENABLE_EDGE)
                    // Use ddx/ddy to calculate depth gradients.
                    float2 grad = float2(ddx(trueDepthMeters), ddy(trueDepthMeters));
                    float depthGrad = length(grad);

                    // Detect depth discontinuities due to scene geometry rather than quantization.
                    if (depthGrad > _EdgeThreshold) {
                        // Render random dropout pixels rather than continuous lines.
                        // Generate a pseudorandom value from the pixel position.
                        float2 noiseUV = uv * _ScreenParams.xy; 
                        #if defined(_ENABLE_DYNAMIC_NOISE)
                            noiseUV += float2(fmod(_Time.y * 60.0, 100.0), 0);
                        #endif
                        
                        // Turn edge pixels black with the configured probability.
                        if (rand(noiseUV) < _EdgeNoiseProb) {
                            isEdgeNoise = true;
                        }
                    }
                #endif

                // ========================================================
                // 2. Quantize display depth without changing the edge detection above.
                // ========================================================
                // Quantized display depth, used for color mapping.
                float displayDepth = trueDepthMeters; 

                #if defined(_ENABLE_QUANTIZATION)
                    if (_QuantizationSteps > 1.0) {
                        // Produce discrete depth levels.
                        displayDepth = floor(trueDepthMeters * _QuantizationSteps) / _QuantizationSteps;
                    }
                #endif

                // ========================================================
                // 3. Speckle dropout (random invalid pixels).
                // ========================================================
                bool isSpeckle = false;
                #if defined(_ENABLE_SPECKLE)
                    float safePixelSize = max(1.0, _NoisePixelSize);
                    float2 screenParams = _ScreenParams.xy;
                    float2 blockUV = floor(uv * screenParams / safePixelSize);
                    #if defined(_ENABLE_DYNAMIC_NOISE)
                        blockUV += float2(fmod(_Time.y * 10.0, 100.0), 0);
                    #endif
                    isSpeckle = rand(blockUV) < _SpeckleProb;
                #endif

                // ========================================================
                // 4. Compose the final visualization.
                // ========================================================
                float3 finalColor = float3(0,0,0);
                
                // Apply the clipping threshold to the unquantized depth in meters.
                bool isClipped = trueDepthMeters > _ClipThreshold;

                // Clipped, edge-dropout and speckle-dropout pixels are all displayed as black.
                if (isClipped || isEdgeNoise || isSpeckle) {
                    finalColor = float3(0, 0, 0); 
                } else {
                    // Map the quantized displayDepth to a color.
                    float t = saturate((displayDepth - _MinDistance) / (_VisualRangeMax - _MinDistance));
                    #if defined(_COLORMAPMODE_RAINBOW)
                        finalColor = HSVtoRGB((1.0 - t) * 240.0, 1.0, 1.0);
                    #else
                        finalColor = lerp(_NearColor.rgb, _FarColor.rgb, t);
                    #endif
                }

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}