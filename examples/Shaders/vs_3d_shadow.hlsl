cbuffer cb : register(b0)
{
    float4x4 mvp; 
    float4x4 shadow; 
}

struct VSIn
{
    float3 position : POSITION; //Position
    float3 normal : NORMAL; //Normal
    float2 uv : TEXCOORD; //Texture coordinate
};

struct PSIn
{
    float4 position : SV_POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD;
    float3 shadowTex : TEXCOORD1;
};
PSIn main(VSIn input)
{
    PSIn output;
    output.texcoord = input.uv;
    output.position = mul(float4(input.position,1), mvp);
    output.normal = input.normal;
    float4 shadowTex =mul(float4(input.position,1), shadow);
    shadowTex.y=-shadowTex.y;
    shadowTex.xy=shadowTex.xy*0.5+0.5;

    output.shadowTex = shadowTex.xyz;

    return output;
}