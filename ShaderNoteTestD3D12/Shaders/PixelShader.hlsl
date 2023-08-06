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
float4 main(PSIn input) : SV_TARGET
{
    //return texture0.Sample(sampler0, input.texcoord);
    return float4(input.texcoord, 1, 1);
    //return float4(color.rgb, 1);
}