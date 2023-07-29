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

Output main(PSIn input) : SV_TARGET
{
    float4 light=(saturate(dot(input.normal,lightDir))*0.5+0.5)*lightColor;
    float4 tex1=texture0.Sample(sampler0,input.texcoord);
    clip(tex1.a-0.01);
    Output output =
    {
        float4((tex1*light).rgb,tex1.a),
        float4(light.rgb,1),
        float4(input.normal*0.5+0.5, 1),
        texture0.Sample(sampler0,input.texcoord),
    };
    return output;
}