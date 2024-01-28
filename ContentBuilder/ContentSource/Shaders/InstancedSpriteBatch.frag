#version 450

layout(location = 0) in vec2 inTexCoord;
layout(location = 1) in vec4 inColor;

layout(location = 0) out vec4 FragColor;

layout(binding = 0, set = 1) uniform sampler2D Sampler;

void main()
{
	vec4 sampledColor = texture(Sampler, inTexCoord);

	if (sampledColor.a < 0.02)
	{
		discard;
	}

	FragColor = texture(Sampler, inTexCoord) * inColor;
}
