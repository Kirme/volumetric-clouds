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

            // Parameters
            float3 boundsMin;
            float3 boundsMax;

            float3 cloudOffset;
            float cloudScale;

            float densityThreshold;
            float densityMultiplier;
            int numSteps;

            Texture3D<float4> ShapeNoise;
            SamplerState samplerShapeNoise;

            // Function adapted from Sebastian Lague https://youtu.be/4QOcCGI6xOU
            // Finds entry and exit point of ray to a box
            float2 rayBoxDist(float3 bmin, float3 bmax, float3 rayOrigin, float3 rayDir) {
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
            float remap(float v, float lOrig, float rOrig, float lNew, float rNew) {
                float nom = (v - lOrig) * (rNew - lNew);
                float denom = rOrig - lOrig;

                return lNew + nom / denom;
            }

            // Function adapted from Sebastian Lague https://youtu.be/4QOcCGI6xOU
            // Samples cloud density at given position
            float sampleDensity(float3 position) {
                float3 newPos = position * cloudScale * 0.001 + cloudOffset * 0.01;
                float4 shape = ShapeNoise.SampleLevel(samplerShapeNoise, newPos, 0);

                float density = max(0, shape.r - densityThreshold) * densityMultiplier;
                return density;
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
                float2 rayBox = rayBoxDist(boundsMin, boundsMax, rayOrigin, rayDir);
                float distToBox = rayBox.x;
                float distInBox = rayBox.y;

                // Return if not hit box
                bool rayHit = distInBox > 0 && distToBox < depth;
                if (!rayHit)
                    return col;

                float distTravelled = 0; // Distance travelled in box
                float stepSize = distInBox / numSteps; // Step size based on number of steps
                float limit = min(depth - distToBox, distInBox);

                float totDensity = 0;
                while (distTravelled < limit) {
                    // Current position on ray
                    float3 pos = rayOrigin + rayDir * (distToBox + distTravelled);
                    
                    // Increase total density
                    totDensity += sampleDensity(pos) * stepSize;

                    // Continue along ray
                    distTravelled += stepSize;
                }

                // Calculate transmittance, how much light gets through based on density
                float transmittance = exp(-totDensity);

                return col * transmittance + (1 - transmittance);
            }
            ENDCG
        }
    }
}
