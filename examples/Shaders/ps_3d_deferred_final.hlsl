cbuffer cb : register(b0)
{
    float4x4 vp;
    float4x4 inverseVP; 
    float4x4 shadow; 
    float4 lightColor;
    float3 lightDir;
}
Texture2D<float4> texture0 : register(t0);
Texture2D<float4> texture1 : register(t1);
Texture2D<float> screenDepth : register(t2);
Texture2D<float> shadowTexture : register(t3);
SamplerState sampler0 : register(s0);

struct PSIn
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};
struct Output
{
    float4 color0 : COLOR0;
};

Output main(PSIn input) : SV_TARGET
{
    float depth=screenDepth.Sample(sampler0,input.texcoord);
    
    float2 uv1=input.texcoord;
    uv1.y=1-uv1.y;
	float4 worldPos = mul(float4(uv1 * 2 - 1, depth, 1), inverseVP);
	worldPos /= worldPos.w;

    float4 diffuseColor= texture0.Sample(sampler0,input.texcoord);
    float3 normal= texture1.Sample(sampler0,input.texcoord).xyz*2-1;

    float4 shadowPos=mul(worldPos,shadow);
    float2 shadowUV=shadowPos.xy*0.5+0.5;
    shadowUV.y=1-shadowUV.y;

    float shadowDepth=shadowTexture.Sample(sampler0,shadowUV);
    float inShadow=(shadowDepth+0.01f<shadowPos.z)?0.0f:1.0f;
    float4 light=(saturate(dot(normal,lightDir))*0.5*inShadow+0.5)*lightColor;

    Output output =
    {
        float4((diffuseColor*light).rgb,diffuseColor.a),
    };
    return output;
}