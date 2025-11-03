using System.Collections.Generic;
using System.Numerics;
using MoonWorks.Graphics;
using RollAndCash.Components;

namespace RollAndCash.Data;

#if DEBUG
public struct FiledEditorLevel
{
    // I'm spamming properties everywhere to make JSON serialize it. 
    // Yes, there's other ways, but this is the way Cosmo & Co. were doing it.
    public string Name { get; set; }
    public Layer[] Layers { get; set; }

    public struct Layer
    {
        public string Name { get; set; }
        public float Depth { get; set; }
        public Editor.LiveEditorLevel.Layer.Types Type { get; set; }
        public Color ColorBlend { get; set; }
        public (string SpriteAnimName, Color)[] Images { get; set; }
        public int ImagesPerRow { get; set; }
        public Entity[] Entities { get; set; }

        public struct Entity
        {
            public Position2D StartPosition { get; set; }
            public Color ColorBlend { get; set; }
            public Prefabs PrefabID { get; set; }
            public string SpriteAnimOverrideName { get; set; }
            public float Angle { get; set; }

            // TODO: Option to delay spawn / start off invisible, if needed.
        }
    }
}
#endif