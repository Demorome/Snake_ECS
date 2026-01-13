using MoonWorks.Graphics;
using RollAndCash.Systems;
using RollAndCash.Data;
using RollAndCash.Messages;
using System.Numerics;
using System;
using RollAndCash.Components;
using System.Text.Json;
using System.Text.Json.Serialization;
using RollAndCash.Content;

namespace RollAndCash.ComponentSerialization;

/// <summary>
/// FIXME: Allow saving & reading as hex string!
/// MUST be registered to JsonSerializerOptions,
/// since we can't directly add it as an attribute to Color.
/// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to#registration-sample---converters-collection
/// </summary>
public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var hexString = reader.GetString();
        return Color.White;
        //return Color.FromHexString(hexString).Value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Color valueToConvert,
        JsonSerializerOptions options)
    {
        // FIXME: Write big-endian value here!!!
        // Based on https://github.com/dotnet/runtime/blob/891c183b22d023eea7bc4aa57dbc219d54852036/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/Int32Converter.cs#L24
        //writer.WriteStringValue(valueToConvert.ToHexString());
    }
}

public class AngleJsonConverter : JsonConverter<Angle>
{
    public override Angle Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // based on https://github.com/dotnet/runtime/blob/891c183b22d023eea7bc4aa57dbc219d54852036/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/SingleConverter.cs#L20
        var angleInDegrees = reader.GetSingle();
        return Angle.FromDegrees(angleInDegrees);
    }

    public override void Write(
        Utf8JsonWriter writer,
        Angle valueToConvert,
        JsonSerializerOptions options)
    {
        var angleInDegrees = valueToConvert.ValueInDegrees;
        //JsonSerializer.Serialize(writer, angleInDegrees, options);
        writer.WriteNumberValue(angleInDegrees);
    }
}

public class SpriteAnimationJsonConverter : JsonConverter<SpriteAnimation>
{
    public override SpriteAnimation Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var spriteAnimName = reader.GetString()!;
        SpriteAnimationInfo? spriteAnimInfo;
        if (SpriteAnimations.NameToInfoMap
            .TryGetValue(spriteAnimName, out spriteAnimInfo))
        {
            return new SpriteAnimation(spriteAnimInfo);
        }

        throw new JsonException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        SpriteAnimation valueToConvert,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(valueToConvert.SpriteAnimationInfo.Name);
    }
}