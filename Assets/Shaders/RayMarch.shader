Shader "Hidden/RayMarch" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _SourceTex ("Texture", 2D) = "white" {} // Texture with original source (used in second it)
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

            sampler2D _SourceTex;

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

            float maxPixelDiff;

            int showInterpolation;

            // Lighting parameters
            float lightAbsorption;
            int numSunSteps;

            // Function adapted from Sebastian Lague https://youtu.be/4QOcCGI6xOU
            // Finds entry and exit point of ray to a box
            float2 RayBoxDist(float3 bmin, float3 bmax, float3 rayOrigin, float3 rayDir) {
                float3 t0 = (bmin - rayOrigin) / rayDir;
                float3 t1 = (bmax - rayOrigin) / rayDir;

                float3 tMin = min(t0, t1); // Box entry point
                float3 tMax = max(t0, t1); // Box exit point

                float dstA = max(max(tMin.x, tMin.y), tMin.z);
                float dstB = min(tMax.x, min(tMax.y, tMax.z));

                float dstToBox = max(0, dstA); // Distance to the box, 0 if in box
                float dstInsideBox = max(0, dstB - dstToBox); // Distance from exit point, 0 if "past" box
                return float2(dstToBox, dstInsideBox);
            }

            /*
            * Function adapted from Fredrik Häggström http://www.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=-8658
            * Remaps v from one range to another
            * v - Value to remap, from [lOrig, rOrig] to [lNew, rNew]
            * lOrig - Original left bound
            * rOrig - Original right bound
            * lNew - New left bound
            * rNew - New right bound
            */ 
            float Remap(float v, float lOrig, float rOrig, float lNew, float rNew) {
                float nom = (v - lOrig) * (rNew - lNew);
                float denom = rOrig - lOrig;

                return lNew + nom / denom;
            }

            // Function adapted from Sebastian Lague https://youtu.be/4QOcCGI6xOU
            // Samples cloud density at given position
            float SampleDensity(float3 position, int miplevel) {
                // Scale and offset position by params
                // Params multiplied by factor to make more manageable in inspector
                float3 newPos = position * cloudScale * 0.001 + cloudOffset * 0.01;

                // Get shape noise at position
                float4 shape = ShapeNoise.SampleLevel(samplerShapeNoise, newPos, miplevel);

                // Current height in cloud box
                float height = (position.y - boundsMin.y) / (boundsMax.y - boundsMin.y);

                // Falloff along x & z in container, avoids hard cutoffs at edges
                const float containerEdgeFadeDst = 50;
                float distFromEdgeX = min(containerEdgeFadeDst, min(position.x - boundsMin.x, boundsMax.x - position.x));
                float distFromEdgeZ = min(containerEdgeFadeDst, min(position.z - boundsMin.z, boundsMax.z - position.z));
                float edgeWeight = min(distFromEdgeZ, distFromEdgeX) / containerEdgeFadeDst;

                // Falloff in y direction of container, avoids hard cutoffs at edges
                // At what height to start and end cutoffs
                float gMin = .2; 
                float gMax = .7;

                float heightGradient = saturate(Remap(height, 0.0, gMin, 0, 1)) * saturate(Remap(height, 1, gMax, 0, 1));
                heightGradient *= edgeWeight;

                // Get FBM by weight params
                float4 normalizedWeights = shapeNoiseWeights / dot(shapeNoiseWeights, 1);
                float shapeFBM = dot(shape, normalizedWeights) * heightGradient;

                // If there is a cloud (i.e. shape noise), determine detail noise
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

                // If we end up here, shapeFBM <= 0 so return 0
                return 0;
            }

            // Function adapted from Fredik Häggström http://umu.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=5365
            // Calculate Beer's law (simplified)
            float BeersLaw(float sunDensity) {
                if (sunDensity > 0)
                    return exp(-lightAbsorption*sunDensity);
                return 1; // Since result is multiplied, 1 is identity
            }

            // Get density toward sun from point in cloud
            float DensityTowardSun(float3 position) {
                float3 lightDir = _WorldSpaceLightPos0.xyz; // Direction to light source
                float distInBox = RayBoxDist(boundsMin, boundsMax, position, 1/lightDir).y;
                float stepSize = distInBox / numSunSteps;

                float density = 0;
                // Step numSunSteps times toward sun
                for (int i = 0; i <= numSunSteps; i++) {
                    // Increasing mip level for each step, reduces performance cost
                    int miplevel = 0.5 * i;

                    position += lightDir * stepSize; // Take one step

                    density += max(0, SampleDensity(position, miplevel) * stepSize); // Calculate density
                }

                return density;
            }
            /*
            * Gets total cloud density along ray
            * rayOrigin - Origin of ray
            * rayDir - Direction of ray
            * distToBox - Current distance to the cloud box
            * distInBox - Current distance inside the box
            * depth - Camera depth
            * blueNoise - Blue noise, used to offset starting position
            */
            float4 GetTransmittance(float3 rayOrigin, float3 rayDir, float distToBox, float distInBox, float depth, float blueNoise) {
                // Max limit is distance to travel in box
                float limit = min(depth - distToBox, distInBox);
                float stepSize = 10; // Constant step size

                // Offset start based on blue noise
                float rayOffset = (blueNoise - 0.5) * 2 * stepSize;
                float distTravelled = rayOffset;

                float totDensity = 0;
                float transmittance = 1;
                float3 lightEnergy = 0;

                while (distTravelled < limit) {
                    // Current position on ray
                    float3 pos = rayOrigin + rayDir * distTravelled;
                    
                    float density = SampleDensity(pos, 0); // Sample density
                    
                    // Only calculate if non-zero density
                    if (density > 0) {
                        float sunDensity = DensityTowardSun(pos); // Calculate density toward sun
                        float lightTransmittance = BeersLaw(sunDensity); // Calculate lighting

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

            bool ShouldExitEarly() {
                return numSteps <= 0;
            }

            // See if we hit the cloud box, and should potentially render clouds
            bool HitBox(v2f i) {
                // Get ray origin and direction
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.viewVector);

                // Gets depth of scene geometry based on camera depth texture
                float nlDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nlDepth) * length(i.viewVector);

                // Get distance to and in cloud box
                float2 rayBox = RayBoxDist(boundsMin, boundsMax, rayOrigin, rayDir);

                // We are not past the box, and the box is in front of the scene geometry
                return rayBox.y > 0 && rayBox.x < depth;
            }

            // Main ray march function
            fixed4 March(v2f i) {
                fixed4 col = tex2D(_SourceTex, i.uv); // Base pixel color

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

                float blueNoise = BlueNoise.SampleLevel(samplerBlueNoise, i.uv, 0);

                // Calculate transmittance and light energy
                float4 transmittance = GetTransmittance(rayOrigin, rayDir, distToBox, distInBox, depth, blueNoise);
                float3 lightEnergy = transmittance.yzw;

                // Return color based on base color, transmittance, and light
                return fixed4(col * transmittance.x + lightEnergy * _LightColor0, 0);
            }

            // Should we evaluate this pixel during this iteration?
            bool ShouldEvaluate(float2 pos, bool firstIt) {
                // Are x and y divisible by march interval?
                bool isFirstPattern = floor(pos.x) % marchInterval == 0 && floor(pos.y) % marchInterval == 0;
                
                // Should evaluate if is first pattern and iteration, or if neither first pattern nor first iteration
                return isFirstPattern == firstIt;
            }

            bool IsWithinScreenParams(float2 pos) {
                bool horizontal = pos.x >= 0 && pos.x < _ScreenParams.x;
                bool vertical = pos.y >= 0 && pos.y < _ScreenParams.y;

                return horizontal && vertical;
            }

            // Is position already ray marched and within the screen parameters?
            bool ShouldInterpolateFrom(float2 pos) {
                return IsWithinScreenParams(pos) && ShouldEvaluate(pos, true);
            }

            // Interpolate between two values with alpha-value, making sure both exist
            fixed4 AddInterpolatedColor(float2 rt, float2 lb, float mult) {
                fixed4 col = float4(0,0,0,0); // Default color
                
                if (ShouldInterpolateFrom(rt)) {
                    col = tex2D(_MainTex, rt);

                    if (ShouldInterpolateFrom(lb)) {
                        // Both are inside grid
                        col *= mult;
                        col += tex2D(_MainTex, lb) * (1 - mult);
                    }

                    // Else only rt inside grid, do nothing more
                    return col;
                }

                if (ShouldInterpolateFrom(lb)) {
                    // Only lb inside grid
                    col = tex2D(_MainTex, lb);
                }

                return col;
            }

            // Modulo that does not give negative result
            float mod(float x, float m) {
                return (x % m + m) % m;
            }

            float2 AddToAvgIfValid(float x, float2 a) {
                if (x != -1) {
                    return float2(a.x + x, a.y + 1);
                }

                return a;
            }

            float AddToSumIfValid(float x, float avg) {
                if (x != -1) {
                    return pow(x - avg, 2);
                }

                return 0;
            }


            // Gets standard deviation of four values a-d
            float StandardDeviation(float a, float b, float c, float d) {
                
                float2 avgAmount = AddToAvgIfValid(a, float2(0,0));
                avgAmount = AddToAvgIfValid(b, avgAmount);
                avgAmount = AddToAvgIfValid(c, avgAmount);
                avgAmount = AddToAvgIfValid(d, avgAmount);

                float amount = avgAmount.y;
                float avg = avgAmount.x / amount;
                
                float sum = AddToSumIfValid(a, avg);
                sum += AddToSumIfValid(b, avg);
                sum += AddToSumIfValid(c, avg);
                sum += AddToSumIfValid(d, avg);

                return sqrt(sum / (amount - 1));
            }

            // Is the stdev lower than max threshold?
            bool MeetsDifferenceThresholdHelper(float rt, float rb, float lt, float lb) {
                return StandardDeviation(rt, rb, lt, lb) < maxPixelDiff;
            }

            // Are the four corners similar enough for us to interpolate?
            bool MeetsDifferenceThreshold(fixed4 rt, fixed4 rb, fixed4 lt, fixed4 lb) {
                // Check condition for each color channel
                return MeetsDifferenceThresholdHelper(rt.r, rb.r, lt.r, lb.r) &&
                       MeetsDifferenceThresholdHelper(rt.g, rb.g, lt.g, lb.g) &&
                       MeetsDifferenceThresholdHelper(rt.b, rb.b, lt.b, lb.b) &&
                       MeetsDifferenceThresholdHelper(rt.a, rb.a, lt.a, lb.a);
            }

            // Check difference threshold only if pixel is not outside screen params
            bool MeetsDifferenceThresholdWithCondition(float2 rt, float2 rb, float2 lt, float2 lb) {
                fixed4 rtc, rbc, ltc, lbc;
                
                if (ShouldInterpolateFrom(rt))
                    rtc = tex2D(_MainTex, rt);
                else
                    rtc = fixed4(-1, -1, -1, -1);

                if (ShouldInterpolateFrom(rb))
                    rbc = tex2D(_MainTex, rb);
                else
                    rbc = fixed4(-1, -1, -1, -1);

                if (ShouldInterpolateFrom(lt))
                    ltc = tex2D(_MainTex, lt);
                else
                    ltc = fixed4(-1, -1, -1, -1);

                if (ShouldInterpolateFrom(lb))
                    lbc = tex2D(_MainTex, lb);
                else
                    lbc = fixed4(-1, -1, -1, -1);

                return MeetsDifferenceThreshold(rtc, rbc, ltc, lbc);
            }

            // Interpolate pixel color based on already ray marched pixels
            fixed4 InterpolateColor(v2f i) {
                float2 pos = i.uv * _MainTex_TexelSize.zw;

                // Remainder when position divided by marching interval
                float xRem = mod(floor(pos.x), marchInterval);
                float yRem = mod(floor(pos.y), marchInterval);

                // Right top
                float2 rt = i.uv + float2(_MainTex_TexelSize.x * (marchInterval - xRem)
                                          , _MainTex_TexelSize.y * (marchInterval - yRem));
                // Right bottom
                float2 rb = i.uv + float2(_MainTex_TexelSize.x * (marchInterval - xRem)
                                          , -_MainTex_TexelSize.y * yRem);
                // Left top
                float2 lt = i.uv + float2(-_MainTex_TexelSize.x * xRem
                                          , _MainTex_TexelSize.y * (marchInterval - yRem));
                // Left bottom
                float2 lb = i.uv - float2(_MainTex_TexelSize.x * xRem
                                          , _MainTex_TexelSize.y * yRem);

                // If not coherent enough, march
                if (!MeetsDifferenceThresholdWithCondition(rt, rb, lt, lb)) {
                    return March(i);
                }

                // alpha-value for x and y
                float xa = xRem / marchInterval;
                float ya = yRem / marchInterval;

                // Perform bilinear interpolation based on alpha-values
                fixed4 bottom = AddInterpolatedColor(rb, lb, xa);
                fixed4 top = AddInterpolatedColor(rt, lt, xa);

                fixed4 newCol = top * ya + bottom * (1 - ya);

                // Should we display which pixels have been interpolated vs ray marched?
                if (showInterpolation) {
                    // Show only green channel (and alpha)
                    return fixed4(0, newCol.g, 0, newCol.a);
                }

                return newCol;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Check that we hit the box
                if (!HitBox(i))
                    return tex2D(_MainTex, i.uv);

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
