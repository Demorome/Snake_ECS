using MoonWorks.Graphics;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash;

public class TriangleBatch
{
	const int MAX_TRI_COUNT = 8192;

	GraphicsDevice GraphicsDevice;
	GraphicsPipeline GraphicsPipeline;

	int InstanceIndex;
	public uint InstanceCount => (uint) InstanceIndex;

	TransferBuffer InstanceTransferBuffer;
	Buffer TriangleVertexBuffer;

	public TriangleBatch(GraphicsDevice graphicsDevice, MoonWorks.Storage.TitleStorage titleStorage, TextureFormat renderTextureFormat, TextureFormat? depthTextureFormat = null)
	{
		GraphicsDevice = graphicsDevice;

		var shaderContentPath = "Content/Shaders";

		var vertShader = ShaderCross.Create(GraphicsDevice, titleStorage, $"{shaderContentPath}/TriangleBatch.vert.hlsl.spv", "main",
			ShaderCross.ShaderFormat.SPIRV, ShaderStage.Vertex);
		var fragShader = ShaderCross.Create(GraphicsDevice, titleStorage, $"{shaderContentPath}/TriangleBatch.frag.hlsl.spv", "main",
			ShaderCross.ShaderFormat.SPIRV, ShaderStage.Fragment);

		var createInfo = new GraphicsPipelineCreateInfo
		{
			TargetInfo = new GraphicsPipelineTargetInfo
			{
				ColorTargetDescriptions = [
					new ColorTargetDescription
					{
						Format = renderTextureFormat,
						BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend
					}
				]
			},
			DepthStencilState = DepthStencilState.Disable,
			MultisampleState = MultisampleState.None,
			PrimitiveType = PrimitiveType.TriangleList,
			RasterizerState = RasterizerState.CCW_CullNone, // FIXME: Not sure if it's actually CCW.
			VertexInputState = VertexInputState.CreateSingleBinding<PositionColorVertex>(),
			VertexShader = vertShader,
			FragmentShader = fragShader,
			Name = "TriangleBatch Pipeline"
		};

		if (depthTextureFormat.HasValue)
		{
			createInfo.TargetInfo.DepthStencilFormat = depthTextureFormat.Value;
			createInfo.TargetInfo.HasDepthStencilTarget = true;

			createInfo.DepthStencilState = new DepthStencilState
			{
				EnableDepthTest = true,
				EnableDepthWrite = true,
				CompareOp = CompareOp.LessOrEqual
			};
		}

		GraphicsPipeline = GraphicsPipeline.Create(
			GraphicsDevice,
			createInfo
		);

		fragShader.Dispose();
		vertShader.Dispose();

		InstanceTransferBuffer = TransferBuffer.Create<TriangleVertexInstanceData>(
			GraphicsDevice,
			"TriangleBatch InstanceTransferBuffer",
			TransferBufferUsage.Upload,
			MAX_TRI_COUNT * 3
		);

		InstanceIndex = 0;

		TriangleVertexBuffer = Buffer.Create<PositionColorVertex>(
			GraphicsDevice,
			"Point Vertex",
			BufferUsageFlags.Vertex,
			MAX_TRI_COUNT * 3
		);
	}

	// Call this before adding connected triangles
	public void Start()
	{
		InstanceIndex = 0;
		InstanceTransferBuffer.Map(true);
	}

	public void AddTriangle(
		Vector4 color,
		Vector3 position1,
		Vector3 position2,
		Vector3 position3
	)
	{
		var instanceDatas = InstanceTransferBuffer.MappedSpan<TriangleVertexInstanceData>();
		instanceDatas[InstanceIndex].Translation = position1;
		instanceDatas[InstanceIndex].Color = color;
		InstanceIndex += 1;

		instanceDatas = InstanceTransferBuffer.MappedSpan<TriangleVertexInstanceData>();
		instanceDatas[InstanceIndex].Translation = position2;
		instanceDatas[InstanceIndex].Color = color;
		InstanceIndex += 1;
		
		instanceDatas = InstanceTransferBuffer.MappedSpan<TriangleVertexInstanceData>();
		instanceDatas[InstanceIndex].Translation = position3;
		instanceDatas[InstanceIndex].Color = color;
		InstanceIndex += 1;
	}
	
	// Call this outside of any pass
	public void Upload(CommandBuffer commandBuffer)
	{
		InstanceTransferBuffer.Unmap();

		if (InstanceCount > 0)
		{
			var copyPass = commandBuffer.BeginCopyPass();
			copyPass.UploadToBuffer(
				new TransferBufferLocation(InstanceTransferBuffer),
				new BufferRegion(TriangleVertexBuffer, 0, (uint)(Marshal.SizeOf<TriangleVertexInstanceData>() * InstanceCount)),
				true
			);
			commandBuffer.EndCopyPass(copyPass);
		}
	}

	public void Render(RenderPass renderPass, ViewProjectionMatrices viewProjectionMatrices)
	{
		renderPass.BindGraphicsPipeline(GraphicsPipeline);
		renderPass.BindVertexBuffers(new BufferBinding(TriangleVertexBuffer, 0));
		renderPass.CommandBuffer.PushVertexUniformData(viewProjectionMatrices.View * viewProjectionMatrices.Projection);

		renderPass.DrawPrimitives(InstanceCount, 1, 0, 0);
	}
}

[StructLayout(LayoutKind.Explicit, Size = 28)]
struct PositionColorVertex : IVertexType
{
	[FieldOffset(0)]
	public Vector3 Position;
	[FieldOffset(12)]
	public Vector4 Color;

	public static VertexElementFormat[] Formats { get; } =
	[
		VertexElementFormat.Float3,
		VertexElementFormat.Float4
	];

	public static uint[] Offsets { get; } =
	[
		0,
		12
	];
}

[StructLayout(LayoutKind.Explicit, Size = 28)]
public record struct TriangleVertexInstanceData
{
	[FieldOffset(0)]
	public Vector3 Translation;
	[FieldOffset(12)]
	public Vector4 Color;
}