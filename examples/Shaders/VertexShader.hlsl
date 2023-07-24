struct VSIn
{
    float3 position : POSITION;
    float2 texcoord : TEXCOORD;
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
    output.position = float4(input.position, 1.0);
    output.texcoord = input.texcoord;

    return output;
}