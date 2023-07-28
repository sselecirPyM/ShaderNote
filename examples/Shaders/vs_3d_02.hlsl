cbuffer cb : register(b0)
{
    float4x4 mvp; 
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
};
PSIn main(VSIn input)
{
    PSIn output;
    output.texcoord = input.uv;
    output.position = mul(float4(input.position,1), mvp);
    output.normal = input.normal;

    return output;
}