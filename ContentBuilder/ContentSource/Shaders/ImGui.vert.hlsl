// Credits to @darkerbit: https://gist.github.com/darkerbit/6bfb661d7ce9263ddd7dcc7b475460e0
struct Input
{
	float2 Position : TEXCOORD0;
	float2 TexCoord : TEXCOORD1;
	float4 Color : TEXCOORD2;
};

struct Output
{
	float2 TexCoord : TEXCOORD0;
	float4 Color : TEXCOORD1;
	float4 Position : SV_Position;
};

cbuffer Uniforms : register(b0, space1)
{
	float4x4 ProjMat : packoffset(c0);
};

Output main(Input input)
{
	Output output;

	output.Position = mul(ProjMat, float4(input.Position, 0.0f, 1.0f));
	output.TexCoord = input.TexCoord;
	output.Color = input.Color;

	return output;
}