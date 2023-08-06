cbuffer cb : register(b0)
{
    float4 color;
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
    float2 texcoord : TEXCOORD;
};
PSIn main(VSIn input)
{
    PSIn output;
    output.texcoord = input.uv;
    output.position = float4(input.position, 1.0);

    return output;
}