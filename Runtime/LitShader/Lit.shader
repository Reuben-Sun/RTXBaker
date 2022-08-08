Shader "RT/Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            
            static const float Pi = 3.141592654f;
            static const float CosineA0 = Pi;
            static const float CosineA1 = (2.0f * Pi) / 3.0f;
            static const float CosineA2 = Pi * 0.25f;
            
            struct SH9  
            {
                float c[9];
            };

             SH9 CalculateCoefficients(float3 normal)
            {
                float A0 = Pi;
                float A1 = (2.0f * Pi) / 3.0f;
                float A2 = Pi * 0.25f;
                
                SH9 sh;
                sh.c[0] = 0.282095 * A0;
                sh.c[1] = 0.488603 * normal.x * A1;
                sh.c[2] = 0.488603 * normal.z * A1;
                sh.c[3] = 0.488603 * normal.y * A1;
                sh.c[4] = 1.092548 * normal.x * normal.z * A2;
                sh.c[5] = 1.092548 * normal.y * normal.z * A2;
                sh.c[6] = 1.092548 * normal.y * normal.x * A2;
                sh.c[7] = (0.946176 * normal.z * normal.z - 0.315392) * A2;
                sh.c[8] = 0.546274 * (normal.x * normal.x - normal.y * normal.y) * A2;
                
                return sh;
            }
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal: NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 normalWS: TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _SHData[9];

            float3 CalculateColor(SH9 coeffs)
            {
                         
                float3 color = coeffs.c[0] * _SHData[0].rgb
            
                             + coeffs.c[1] * _SHData[1].rgb
                             + coeffs.c[2] * _SHData[2].rgb
                             + coeffs.c[3] * _SHData[3].rgb
                             
                             + coeffs.c[4] * _SHData[4].rgb
                             + coeffs.c[5] * _SHData[5].rgb
                             + coeffs.c[6] * _SHData[6].rgb
                             + coeffs.c[7] * _SHData[7].rgb
                             + coeffs.c[8] * _SHData[8].rgb;
                return color;
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                SH9 coeffs = CalculateCoefficients(i.normalWS);
                float3 diffuse = CalculateColor(coeffs) / Pi;
                return float4(diffuse, 1);
            }
            ENDCG
        }
    }
}
