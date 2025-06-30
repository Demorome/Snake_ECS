cbuffer UniformBlock : register(b0, space3)
{
    float4 MyColor : packoffset(c0);
};

float4 main(float2 TexCoord : TEXCOORD0) : SV_Target0
{
    return MyColor;
}
