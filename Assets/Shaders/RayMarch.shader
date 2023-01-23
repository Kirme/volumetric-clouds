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

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0));

                return o;
            }

            float3 boundsMin;
            float3 boundsMax;

            Texture3D<float4> shapeNoise;

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv);

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.viewVector);

                float nlDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nlDepth) * length(i.viewVector);

                float2 rayBox = rayBoxDist(boundsMin, boundsMax, rayOrigin, rayDir);
                float distToBox = rayBox.x;
                float distInBox = rayBox.y;

                bool rayHit = distInBox > 0 && distToBox < depth;
                if (rayHit)
                    col = 0;

                return col;
            }
            ENDCG
        }
    }
}
