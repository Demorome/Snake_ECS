using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Input;
using Snake.Relations;
using Snake.Messages;
using Snake.Components;
using System;
using MoonWorks.Graphics;

namespace Snake.Systems;

public class AssignTilePixelPositions : MoonTools.ECS.System
{
	MoonTools.ECS.Filter NeedsPixelPositionFilter { get; }

    TileGrid TileGrid;

	public AssignTilePixelPositions(World world, TileGrid tileGrid) : base(world)
	{
        TileGrid = tileGrid;
		NeedsPixelPositionFilter = FilterBuilder.Include<TilePosition>().Exclude<PixelPosition>().Build();
    }

	public override void Update(TimeSpan timeSpan)
	{
        foreach (var entity in NeedsPixelPositionFilter.Entities)
        {
            var tilePos = Get<TilePosition>(entity).PositionVector;

            TileGrid.Grid[(int)tilePos.X, (int)tilePos.Y] = entity;
            Set(entity, new PixelPosition(GridInfo.TilePositionToPixelPosition(tilePos)));
        }
	}
}


