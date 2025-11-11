using System.Collections.Generic;
using System.Numerics;
using MoonWorks.Graphics;
using RollAndCash.Components;

namespace RollAndCash.Data;

// NOTE: NEVER change the ordering here!!
public enum EntityExtraDataTypes
{
    ColorBlendOverride = 0,
    SpriteAnim,
    AngleOverride,
    DepthOverride, // TODO: Make a separate enum for optimized filed entities, since this isn't needed otherwise!
}

#if DEBUG
public struct FiledEditorLevel
{
    // I'm spamming properties everywhere to make JSON serialize it. 
    // Yes, there's other ways, but this is the way Cosmo & Co. were doing it.
    public int SerializedVersion;
    public string Name { get; set; }
    public Layer[] Layers { get; set; }

    public struct Layer
    {
        public string Name { get; set; }
        public float Depth { get; set; }
        public Editor.LiveEditorLevel.Layer.Types TypeID { get; set; }
        public Color ColorBlend { get; set; }
        public (string SpriteAnimName, Color)[] Images { get; set; }
        public int ImagesPerRow { get; set; }
        public Entity[] Entities { get; set; }

        public struct Entity
        {
            public Prefabs PrefabID { get; set; }
            public Position2D StartPosition { get; set; }
            public Dictionary<EntityExtraDataTypes, object> ExtraDataList { get; set; }
        }
    }
}
#endif