cbuffer cb : register(b0)
{
    float4 color;
}
Texture2D<float4> texture0 : register(t0);
SamplerState sampler0 : register(s0);

struct PSIn
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};
struct Output
{
    float4 color0 : COLOR0;
    float4 color1 : COLOR1;
};

//float4 main(PSIn input) : SV_TARGET
//{
//    //return texture0.Sample(sampler0, input.texcoord);
//    return float4(input.texcoord * color.rg, color.b, 1);
//}
Output main(PSIn input) : SV_TARGET
{
    //return texture0.Sample(sampler0, input.texcoord);
    Output output =
    {
        float4(input.texcoord * color.rg, color.b, 1),
        float4( color.r, input.texcoord *color.gb, 1)
    };
    return output;
}