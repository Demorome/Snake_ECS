using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoonWorks.Graphics;
using MoonWorks.Storage;

namespace RollAndCash.Data;

[JsonSerializable(typeof(CramTextureAtlasData))]
internal partial class CramTextureAtlasDataContext : JsonSerializerContext
{
}

public static class CramAtlasReader
{
	static JsonSerializerOptions options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	static CramTextureAtlasDataContext context 
		= new CramTextureAtlasDataContext(options);

	public static void ReadTextureAtlas(
		GraphicsDevice graphicsDevice, 
		TexturePage texturePage,
		TitleStorage storage)
	{
        var data = (CramTextureAtlasData)TitleStorageExt.DeserializeJson(
			storage,
			texturePage.PartialJsonFilePath, 
			typeof(CramTextureAtlasData), 
			context
		)!;
		texturePage.Load(graphicsDevice, data);
	}
}
