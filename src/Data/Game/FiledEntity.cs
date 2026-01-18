using System.Text.Json.Serialization;

using RollAndCash.Components;

namespace RollAndCash.Data;

public struct FiledEntity
{
    // Usually null, unless we wanted to name an entity in particular in the editor.
    // TODO: Could be made debug-only, but that might remove useful debugging info for release builds.
    [JsonPropertyName("Name")]
    public string UniqueTag;

    [JsonPropertyName("Pos")]
    public Position2D PositionRelativeToRoom;

    [JsonPropertyName("MaybeSpawnInfo")]
    public PrefabSpawnInfo_Filed? MaybeSpawnInfo;

    [Flags]
    public enum Flags
    {
        None    = 0,
        FlipX   = 1 << 0,
        FlipY   = 1 << 1,
    }

    // Null represents that we don't override 
    // the default spawn flags for the prefab.
    // Can't use 0 for that, since that represents overriding w/ 0.
    [JsonPropertyName("Flags")]
    public Flags? MaybeSpawnFlags;

    [JsonPropertyName("Overrides")]
    public PrefabSpawnInfoOverride? MaybeSpawnInfoOverrides;
}