Shader "Hidden/RayMarch" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 viewVector : TEXCOORD1;
                float2 screenPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            sampler2D _CameraDepthTexture;

            // Noise
            Texture3D<float4> ShapeNoise;
            SamplerState samplerShapeNoise;

            Texture3D<float4> DetailNoise;
            SamplerState samplerDetailNoise;

            Texture2D<float4> BlueNoise;
            SamplerState samplerBlueNoise;

            // Parameters
            int useInterpolation;
            int isFirstIteration;
            int marchInterval;
            
            float3 boundsMin;
            float3 boundsMax;

            float3 cloudOffset;
            float cloudScale;

            float3 detailOffset;
            float detailScale;

            float densityThreshold;
            float densityMultiplier;
            int numSteps;

            float4 shapeNoiseWeights;
            float3 detailNoiseWeights;

            float coherence;

            // Lighting parameters
            float lightAbsorption;
            int numSunSteps;

            // Function adapted from Sebastian Lague https://youtu.be/4QOcCGI6xOU
            // Finds entry and exit point of ray to a box
            float2 RayBoxDist(float3 bmin, float3 bmax, float3 rayOrigin, float3 rayDir) {
                float3 t0 = (bmin - rayOrigin) / rayDir;
                float3 t1 = (bmax - rayOrigin) / rayDir;

                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);

                float dstA = max(max(tMin.x, tMin.y), tMin.z);
                float dstB = min(tMax.x, min(tMax.y, tMax.z));

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            // Function adapted from Fredrik Häggström http://www.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=-8658
            // Remaps v from one range to another
            float Remap(float v, float lOrig, float rOrig, float lNew, float rNew) {
                float nom = (v - lOrig) * (rNew - lNew);
                float denom = rOrig - lOrig;

                return lNew + nom / denom;
            }

            float CombineNoise(float4 shape) {
                float lOrig = shape.g * 0.625 + shape.b * 0.25 + shape.a * 0.125 - 1;

                return Remap(shape.r, lOrig, 1, 0, 1);
            }

            // Function adapted from Sebastian Lague https://youtu.be/4QOcCGI6xOU
            // Samples cloud density at given position
            float SampleDensity(float3 position, int miplevel) {
                // Scale and offset position by params
                float3 newPos = position * cloudScale * 0.001 + cloudOffset * 0.01;

                // Get shape noise at position
                float4 shape = ShapeNoise.SampleLevel(samplerShapeNoise, newPos, miplevel);

                // Current height in cloud box
                float height = (position.y - boundsMin.y) / (boundsMax.y - boundsMin.y);

                // Falloff along x & z in container
                const float containerEdgeFadeDst = 50;
                float distFromEdgeX = min(containerEdgeFadeDst, min(position.x - boundsMin.x, boundsMax.x - position.x));
                float distFromEdgeZ = min(containerEdgeFadeDst, min(position.z - boundsMin.z, boundsMax.z - position.z));
                float edgeWeight = min(distFromEdgeZ, distFromEdgeX) / containerEdgeFadeDst;

                // Falloff in y direction of container
                float gMin = .2;
                float gMax = .7;
                float heightGradient = saturate(Remap(height, 0.0, gMin, 0, 1)) * saturate(Remap(height, 1, gMax, 0, 1));
                heightGradient *= edgeWeight;

                // Get FBM by weight params
                float4 normalizedWeights = shapeNoiseWeights / dot(shapeNoiseWeights, 1);
                float shapeFBM = dot(shape, normalizedWeights) * heightGradient;

                if (shapeFBM > 0) {
                    // Get detail noise, same process as shape noise
                    float3 newDetailPos = position * detailScale * 0.001 + detailOffset * 0.01;
                    float4 detail = DetailNoise.SampleLevel(samplerDetailNoise, newDetailPos, miplevel);

                    float3 normalizedDetailWeights = detailNoiseWeights / dot(detailNoiseWeights, 1);
                    float detailFBM = dot(detail, float4(detailNoiseWeights, 0));

                    // Combine shape and detail noise
                    float density = (shapeFBM + detailFBM - densityThreshold) * densityMultiplier;
                    return density;
                }

                return 0;
            }

            // Function adapted from Fredik Häggström http://umu.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=5365
            // Calculate Beer's law (simplified)
            float BeersLaw(float sunDensity) {
                if (sunDensity > 0)
                    return exp(-lightAbsorption*sunDensity);
                return 1;
            }

            // Get density toward sun from point in cloud
            float DensityTowardSun(float3 position) {
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float distInBox = RayBoxDist(boundsMin, boundsMax, position, 1/lightDir).y;
                float stepSize = distInBox / numSunSteps;

                float density = 0;
                // Step numSunSteps times toward sun, meaning num+1 sample points
                for (int i = 0; i <= numSunSteps; i++) {
                    // Increasing mip level for each step, reduces performance cost
                    int miplevel = 0.5 * i;

                    position += lightDir * stepSize;

                    density += max(0, SampleDensity(position, miplevel) * stepSize);
                }

                return density;
            }

            // Gets total cloud density along ray
            float4 GetTransmittance(float3 rayOrigin, float3 rayDir, float distToBox, float distInBox, float depth, float blueNoise) {
                float limit = min(depth - distToBox, distInBox);
                float stepSize = 10; // Constant step size
                float rayOffset = (blueNoise - 0.5) * 2 * stepSize;
                float distTravelled = rayOffset;

                float totDensity = 0;

                float transmittance = 1;
                float3 lightEnergy = 0;

                while (distTravelled < limit) {
                    // Current position on ray
                    float3 pos = rayOrigin + rayDir * distTravelled;
                    
                    float density = SampleDensity(pos, 0);
                    
                    // Only calculate if non-zero density
                    if (density > 0) {
                        float sunDensity = DensityTowardSun(pos);
                        float lightTransmittance = BeersLaw(sunDensity);

                        lightEnergy += density * stepSize * transmittance * lightTransmittance;
                        transmittance *= exp(-density * stepSize);
                    }

                    // Early exit
                    if (transmittance < 0.01)
                        break;

                    // Continue along ray
                    distTravelled += stepSize;
                }

                return float4(transmittance, lightEnergy);
            }

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0));
                o.screenPos = ComputeScreenPos(o.vertex);

                return o;
            }

            float2 squareUV(float2 uv) {
                float width = _ScreenParams.x;
                float height = _ScreenParams.y;

                float scale = 1000;
                float x = uv.x * width;
                float y = uv.y * height;
                return float2 (x / scale, y / scale);
            }

            bool ShouldExitEarly() {
                return numSteps <= 0;
            }

            fixed4 March(v2f i) {
                fixed4 col = tex2D(_MainTex, i.uv);

                if (ShouldExitEarly())
                    return col;

                // Get ray origin and direction
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.viewVector);

                // Gets depth based on camera depth texture
                float nlDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nlDepth) * length(i.viewVector);

                // Get distance to and in cloud box
                float2 rayBox = RayBoxDist(boundsMin, boundsMax, rayOrigin, rayDir);
                float distToBox = rayBox.x;
                float distInBox = rayBox.y;

                // Return if not hit box
                bool rayHit = distInBox > 0 && distToBox < depth;
                if (!rayHit)
                    return col;

                float blueNoise = BlueNoise.SampleLevel(samplerBlueNoise, i.uv, 0);

                // Calculate transmittance and light energy
                float4 transmittance = GetTransmittance(rayOrigin, rayDir, distToBox, distInBox, depth, blueNoise);
                float3 lightEnergy = transmittance.yzw;

                return fixed4(col * transmittance.x + lightEnergy * _LightColor0, 0);
            }

            // Should we evaluate this pixel in this iteration?
            bool ShouldEvaluate(float2 pos, bool firstIt) {
                bool isFirstPattern = floor(pos.x) % marchInterval == 0 && floor(pos.y) % marchInterval == 0;
                
                return isFirstPattern == firstIt;
            }

            // Is position already ray marched and within the screen parameters?
            bool ShouldInterpolateFrom(float2 pos, bool horizontal) {
                bool ret = false;
                
                if (horizontal)
                    ret = pos.x >= 0 && pos.x < _ScreenParams.x;
                else
                    ret = pos.y >= 0 && pos.y < _ScreenParams.y;

                return ret && ShouldEvaluate(pos, true);
            }

            // Interpolate between two values with alpha-value, making sure both exist
            fixed4 AddInterpolatedColor(float2 rt, float2 lb, float mult, bool horizontal) {
                fixed4 col = float4(0,0,0,0); // Default color
                
                if (ShouldInterpolateFrom(rt, horizontal)) {
                    col = tex2D(_MainTex, rt);

                    if (ShouldInterpolateFrom(lb, horizontal)) {
                        // Both are inside grid
                        col *= mult;
                        col += tex2D(_MainTex, lb) * (1 - mult);
                    }

                    // Else only rt inside grid, do nothing more
                    return col;
                }

                if (ShouldInterpolateFrom(lb, horizontal)) {
                    // Only lb inside grid
                    col = tex2D(_MainTex, lb);
                }

                return col;
            }

            // Modulo that does not give negative result
            float mod(float x, float m) {
                return (x % m + m) % m;
            }

            // Interpolate pixel color based on already ray marched pixels
            fixed4 InterpolateColor(v2f i) {
                float2 pos = i.uv * _MainTex_TexelSize.zw;

                float xRem = mod(floor(pos.x), marchInterval);
                float yRem = mod(floor(pos.y), marchInterval);

                float2 rt = i.uv + float2(_MainTex_TexelSize.x * (marchInterval - xRem)
                                             , _MainTex_TexelSize.y * (marchInterval - yRem));
                float2 lb = i.uv - float2(_MainTex_TexelSize.x * xRem
                                            , _MainTex_TexelSize.y * yRem);
                float2 rb = i.uv + float2(_MainTex_TexelSize.x * (marchInterval - xRem)
                                          , -_MainTex_TexelSize.y * yRem);
                float2 lt = i.uv + float2(-_MainTex_TexelSize.x * xRem
                                              , _MainTex_TexelSize.y * (marchInterval - yRem));

                // alpha-value for x and y
                float xa = xRem / marchInterval;
                float ya = yRem / marchInterval;

                fixed4 bottom = AddInterpolatedColor(rb, lb, xa, true);
                fixed4 top = AddInterpolatedColor(rt, lt, xa, true);

                fixed4 newCol = top * ya + bottom * (1 - ya);

                return newCol;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Normal raymarch
                if (!useInterpolation) {
                    return March(i);
                }
                
                float2 pixelPos = i.uv * _MainTex_TexelSize.zw;

                if (ShouldEvaluate(pixelPos, isFirstIteration)) {
                    if (isFirstIteration) { // We should just ray march
                        return March(i);
                    }
                    else { // Interpolate the color
                        return InterpolateColor(i);
                    }
                }

                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
