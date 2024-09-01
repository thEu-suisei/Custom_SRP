Shader "Custom RP/MyComputeShader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 col : COLOR0;
            };
            
            struct particleData
            {
                float3 pos;
                float4 color;
            };

            StructuredBuffer<particleData> _particleDataBuffer;

            //SV_VertexID：在VertexShader中用它来作为传递进来的参数，代表顶点的下标。
            //我们有多少个粒子即有多少个顶点。顶点数据使用我们在Compute Shader中处理过的Buffer。
            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(float4(_particleDataBuffer[id].pos, 0));
                o.col = _particleDataBuffer[id].color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.col;
            }
            ENDCG
        }
    }
}
