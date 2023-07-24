cbuffer cb : register(b0)
{
    float4x4 mvp; 
    float4 lightColor;
    float3 lightDir;
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
    float4 color2 : COLOR2;
    float4 color3 : COLOR3;
};

//float4 main(PSIn input) : SV_TARGET
//{
//    //return texture0.Sample(sampler0, input.texcoord);
//    return float4(input.texcoord * color.rg, color.b, 1);
//}
Output main(PSIn input) : SV_TARGET
{
    //return texture0.Sample(sampler0, input.texcoord);
    float4 light=saturate(dot(input.normal,lightDir))*lightColor+0.5;
    Output output =
    {
        float4((texture0.Sample(sampler0,input.texcoord)*light).rgb,1),
        light,
        float4(input.normal*0.5+0.5, 1),
        texture0.Sample(sampler0,input.texcoord),
    };
    return output;
}