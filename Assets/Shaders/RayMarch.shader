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
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _CameraDepthTexture;

            Texture3D<float4> ShapeNoise;
            SamplerState samplerShapeNoise;

            // Parameters
            float3 boundsMin;
            float3 boundsMax;

            float3 cloudOffset;
            float cloudScale;

            float densityThreshold;
            float densityMultiplier;
            int numSteps;

            float gc;

            float4 shapeNoiseWeights;

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

            // Function adapted from Fredrik H�ggstr�m http://www.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=-8658
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
                float3 newPos = position * cloudScale * 0.001 + cloudOffset * 0.01;

                float4 shape = ShapeNoise.SampleLevel(samplerShapeNoise, newPos, miplevel);

                float height = (position.y - boundsMin.y) / (boundsMax.y - boundsMin.y);

                // Calculate falloff at along x/z edges of the cloud container
                const float containerEdgeFadeDst = 50;
                float distFromEdgeX = min(containerEdgeFadeDst, min(position.x - boundsMin.x, boundsMax.x - position.x));
                float distFromEdgeZ = min(containerEdgeFadeDst, min(position.z - boundsMin.z, boundsMax.z - position.z));
                float edgeWeight = min(distFromEdgeZ, distFromEdgeX) / containerEdgeFadeDst;

                float gMin = .2;
                float gMax = .7;
                float heightGradient = saturate(Remap(height, 0.0, gMin, 0, 1)) * saturate(Remap(height, 1, gMax, 0, 1));
                heightGradient *= edgeWeight;

                float4 normalizedWeights = shapeNoiseWeights / dot(shapeNoiseWeights, 1);
                float shapeFBM = dot(shape, normalizedWeights) * heightGradient;

                //float combined = CombineNoise(shape);

                float density = max(0, shapeFBM - densityThreshold) * densityMultiplier;
                return density;
            }

            // Function adapted from Fredik H�ggstr�m http://umu.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=5365
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
                    int miplevel = 0.5 * i;

                    position += lightDir * stepSize;

                    density += max(0, SampleDensity(position, miplevel) * stepSize);
                }

                return density;
            }

            // Gets total cloud density along ray
            float4 GetTransmittance(float3 rayOrigin, float3 rayDir, float distToBox, float distInBox, float depth) {
                float distTravelled = 0;
                float stepSize = distInBox / numSteps; // Step size based on number of steps
                float limit = min(depth - distToBox, distInBox);

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

                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv);

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

                // Calculate transmittance and light energy
                float4 transmittance = GetTransmittance(rayOrigin, rayDir, distToBox, distInBox, depth);
                float3 lightEnergy = transmittance.yzw;
                //return col * transmittance + (1 - transmittance);

                return fixed4(col * transmittance.x + lightEnergy * _LightColor0, 0);
            }
            ENDCG
        }
    }
}
