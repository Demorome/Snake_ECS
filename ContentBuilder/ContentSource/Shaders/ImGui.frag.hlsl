// Credits to @darkerbit: https://gist.github.com/darkerbit/6bfb661d7ce9263ddd7dcc7b475460e0
struct Input
{
	float2 TexCoord : TEXCOORD0;
	float4 Color : TEXCOORD1;
};

struct Output
{
	float4 Color : SV_Target0;
};

Texture2D<float4> Texture : register(t0, space2);
SamplerState Sampler : register(s0, space2);

Output main(Input input)
{
	Output output;

	output.Color = Texture.Sample(Sampler, input.TexCoord) * input.Color;

	return output;
}