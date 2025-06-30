using MoonWorks.Graphics;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash;

public class ConnectedPointsBatch
{
	const int MAX_POINT_COUNT = 8192;

	GraphicsDevice GraphicsDevice;
	GraphicsPipeline GraphicsPipeline;

	int InstanceIndex;
	public uint InstanceCount => (uint) InstanceIndex;

	TransferBuffer InstanceTransferBuffer;
	Buffer PointVertexBuffer;
	
	List<PointBatchInfo> PointBatchInfos = new();
	int BatchIndex;
	public uint BatchCount => (uint)BatchIndex;
	int LastBatchAmount;

	public ConnectedPointsBatch(GraphicsDevice graphicsDevice, MoonWorks.Storage.TitleStorage titleStorage, TextureFormat renderTextureFormat, TextureFormat? depthTextureFormat = null)
	{
		GraphicsDevice = graphicsDevice;

		var shaderContentPath = "Content/Shaders";

		var vertShader = ShaderCross.Create(GraphicsDevice, titleStorage, $"{shaderContentPath}/ConnectedPointsBatch.vert.hlsl.spv", "main",
			ShaderCross.ShaderFormat.SPIRV, ShaderStage.Vertex);
		var fragShader = ShaderCross.Create(GraphicsDevice, titleStorage, $"{shaderContentPath}/ConnectedPointsBatch.frag.hlsl.spv", "main",
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
			PrimitiveType = PrimitiveType.LineStrip,
			RasterizerState = RasterizerState.CW_CullNone, // FIXME: Not sure if it's actually CCW.
			VertexInputState = VertexInputState.CreateSingleBinding<PositionVertex>(),
			VertexShader = vertShader,
			FragmentShader = fragShader,
			Name = "ConnectedPointsBatch Pipeline"
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

		InstanceTransferBuffer = TransferBuffer.Create<PointInstanceData>(
			GraphicsDevice,
			"ConnectedPointsBatch InstanceTransferBuffer",
			TransferBufferUsage.Upload,
			MAX_POINT_COUNT
		);

		InstanceIndex = 0;
		BatchIndex = 0;
		LastBatchAmount = 0;

		PointVertexBuffer = Buffer.Create<PositionVertex>(
			GraphicsDevice,
			"Point Vertex",
			BufferUsageFlags.Vertex,
			MAX_POINT_COUNT
		);
	}

	// Call this before adding connected points
	public void Start()
	{
		InstanceIndex = 0;
		BatchIndex = 0;
		LastBatchAmount = 0;
		InstanceTransferBuffer.Map(true);
		PointBatchInfos.Clear();
	}

	// Add connected points to the batch
	public void AddPoint(
		Vector3 position
	)
	{
		var instanceDatas = InstanceTransferBuffer.MappedSpan<PointInstanceData>();
		instanceDatas[InstanceIndex].Translation = position;
		InstanceIndex += 1;
	}

	public void RecordPointBatch(Color color)
	{
		PointBatchInfos.Add(new PointBatchInfo(color, InstanceIndex - LastBatchAmount));
		++BatchIndex;
		LastBatchAmount = InstanceIndex;
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
				new BufferRegion(PointVertexBuffer, 0, (uint)(Marshal.SizeOf<PointInstanceData>() * InstanceCount)), 
				true
			);
			commandBuffer.EndCopyPass(copyPass);
		}
	}

	public void Render(RenderPass renderPass, ViewProjectionMatrices viewProjectionMatrices)
	{
		renderPass.BindGraphicsPipeline(GraphicsPipeline);
		renderPass.BindVertexBuffers(new BufferBinding(PointVertexBuffer, 0));
		renderPass.CommandBuffer.PushVertexUniformData(viewProjectionMatrices.View * viewProjectionMatrices.Projection);

		int firstVertex = 0;

		for (int i = 0; i < BatchCount; ++i)
		{
			var batchInfo = PointBatchInfos[i];

			renderPass.CommandBuffer.PushFragmentUniformData(batchInfo.Color.ToVector4());
			renderPass.DrawPrimitives((uint)batchInfo.NumVertices, 1, (uint)firstVertex, 0);

			firstVertex += batchInfo.NumVertices;
		}
	}
}

[StructLayout(LayoutKind.Explicit, Size = 12)]
struct PositionVertex : IVertexType
{
	[FieldOffset(0)]
	public Vector3 Position;

	public static VertexElementFormat[] Formats { get; } =
	[
		VertexElementFormat.Float3
	];

	public static uint[] Offsets { get; } =
	[
		0
	];
}

[StructLayout(LayoutKind.Explicit, Size = 12)]
public record struct PointInstanceData
{
	[FieldOffset(0)]
	public Vector3 Translation;
}

public readonly record struct PointBatchInfo(Color Color, int NumVertices);