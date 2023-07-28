cbuffer cb : register(b0)
{
    float4x4 mvp; 
}
Texture2D<float4> texture0 : register(t0);
SamplerState sampler0 : register(s0);

struct PSIn
{
    float4 position : SV_POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD;
};
struct Output
{
    float4 color0 : COLOR0;
    float4 color1 : COLOR1;
};

Output main(PSIn input) : SV_TARGET
{
    float4 tex1=texture0.Sample(sampler0,input.texcoord);
    Output output =
    {
        tex1,
        float4(input.normal*0.5+0.5, 1),
    };
    return output;
}