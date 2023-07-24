cbuffer cb : register(b0)
{
    float4 color;
}

struct VSIn
{
    float3 position : POSITION;
    uint vertexId : SV_VertexID;
};

struct PSIn
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};
PSIn main(VSIn input)
{
    PSIn output;
    output.texcoord = float2(input.vertexId & 1, (input.vertexId >> 1) & 1);
    //output.position = float4(output.texcoord.xy * 1.0 - 0.5, 0.0, 1.0);
    output.position = float4(input.position, 1.0);

    return output;
}