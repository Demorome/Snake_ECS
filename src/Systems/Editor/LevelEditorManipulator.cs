using System;
using System.Collections.Generic;
using System.Numerics;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Relations;
using RollAndCash.Systems;

namespace RollAndCash.Editor;

public class LevelEditorManipulator : MoonTools.ECS.Manipulator
{
    EditorSystem EditorSystem;
    TileManipulator TileManipulator;
    PrefabManipulator PrefabManipulator;

    public LevelEditorManipulator(World world, EditorSystem editorSystem) : base(world)
    {
        EditorSystem = editorSystem;
        TileManipulator = new(World);
        PrefabManipulator = new(World);
    }

    public static bool IsInLevelEditor = false;
    public bool HasSelectedPrefab = false;
    static bool SnapToGrid = true;
    public static bool ShowGrid = true;
    public Vector2? HoveredOverTilePosition = null;
    public static Vector4 GridLineColor = (Color.DarkTurquoise * 0.5f).ToVector4();

    // Layout inspired by Elias Daler's tutorial series: https://edw.is/using-imgui-with-sfml-pt1/
    void DrawLevelEditorMainWindow()
    {
        bool stillOpened = IsInLevelEditor;
        if (ImGui.Begin("Level Editor", ref stillOpened))
        {
            //FIXME: ImGui.Text("Level path: ");
            //FIXME: ImGui.Text("Camera: ");
            ImGui.Text($"Mouse world position: {Input.WorldMousePosition}");
            ImGui.Text($"Tile position: {TileManipulator.GetTilePos(Input.WorldMousePosition)}");

            ImGui.Checkbox("Show Grid?", ref ShowGrid);
            ImGui.ColorEdit4("Grid Line Color", ref GridLineColor);

            // TODO: Snap to grid option? Not sure if I should support going off-grid yet.
            //ImGui.Checkbox("Snap to Grid", ref SnapEntitiesToGrid);
        }
        ImGui.End();

        if (!stillOpened)
        {
            IsInLevelEditor = false;
        }
    }

    public void HandleLevelEditor(Entity debugEntity)
    {
        if (!IsInLevelEditor)
        {
            return;
        }

        HasSelectedPrefab = PrefabManipulator.ShowPrefabSpawner(debugEntity);

        DrawLevelEditorMainWindow();
        ShowLevelLayerOptions(debugEntity);

        // Layer painting controls.
        var mouseHoveringOverAnyWindow = ImGui.GetIO().WantCaptureMouse;
        if (mouseHoveringOverAnyWindow)
        {
            return;
        }
        var mouseWorldPos = Input.WorldMousePosition;
        HoveredOverTilePosition = TileManipulator.GetTilePos(mouseWorldPos);
        if (!HoveredOverTilePosition.HasValue)
        {
            return;
        }
        if (float.IsNaN(OpenedLayerDepth))
        {
            return;
        }
        var activeLayer = LevelLayers[OpenedLayerDepth];
        var imagesToPaint = GetLayerImagesToPaint();
        if (!HasSelectedPrefab && imagesToPaint != null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            List<Entity> paintedEntities = new();

            switch (activeLayer.LayerType)
            {
                case LevelLayer.LevelLayerTypes.VisualTile:
                case LevelLayer.LevelLayerTypes.SolidTile:
                    if (imagesToPaint.Count == 1 || ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        foreach (var (imageSprite, imageColor, imagePos, layerImageID) in imagesToPaint)
                        {
                            var tileWorldPos = TileManipulator.TilePosToWorldPos_Centered(HoveredOverTilePosition.Value);

                            // Replace a tile if one is already painted in at this level layer.
                            bool spawn = true;
                            foreach (var entity in activeLayer.CachedEntities)
                            {
                                if (Get<Position2D>(entity) == tileWorldPos)
                                {
                                    if (Get<Editor_LayerImageID>(entity) != layerImageID)
                                    {
                                        // FIXME: Undo/Redo support! How to group this change w/ the creation of the tile below?
                                        Destroy(entity);
                                    }
                                    else
                                    {
                                        spawn = false;
                                    }
                                    // Assume there can't be any other entities on this layer at this tile pos.
                                    break;
                                }
                            }

                            if (spawn)
                            {
                                Entity newEntity;
                                if (activeLayer.LayerType == LevelLayer.LevelLayerTypes.SolidTile)
                                {
                                    newEntity = TileManipulator.SpawnSolidTile(tileWorldPos, imageSprite);
                                }
                                else
                                {
                                    newEntity = CreateEntity("Visual Tile");
                                    Set(newEntity, tileWorldPos);
                                    Set(newEntity, imageSprite);
                                }

                                Set(newEntity, new ColorBlend(imageColor));
                                Set(newEntity, imagePos);
                                Set(newEntity, layerImageID);

                                paintedEntities.Add(newEntity);
                            }
                        }
                    }
                    break;
                case LevelLayer.LevelLayerTypes.Image:
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        var (spriteAnim, color, position, layerImageID) = imagesToPaint[0];

                        Entity newEntity = CreateEntity("Image");
                        Set(newEntity, spriteAnim);
                        Set(newEntity, new ColorBlend(color));
                        Set(newEntity, position);
                        Set(newEntity, layerImageID);
                    }
                    break;
            }

            foreach (var entity in paintedEntities)
            {
                Set(entity, new Depth(OpenedLayerDepth));

                // FIXME: Group together multiple entities created in a single paintbrush stroke for Undo.
                UndoRedo.StoreEntityCreateHistory(entity, World);
            }
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            // Delete tiles on this level layer!
            if (activeLayer.IsTiled || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                var mouseHitboxRect = new Rectangle(0, 0, 1, 1);
                var mouseWorldPosRect = mouseHitboxRect.GetWorldRect(mouseWorldPos);

                // Hopefully won't need an acceleration structure for this...
                foreach (var entity in activeLayer.CachedEntities)
                {
                    var rect = EditorSystem.GetEntityVisualRect(entity);
                    var worldRect = rect.Value.GetWorldRect(Get<Position2D>(entity));

                    if (worldRect.Intersects(mouseWorldPosRect))
                    {
                        // FIXME: Group together deletions done while holding the mouse down!
                        UndoRedo.StoreEntityDestroyHistory(entity, World);
                        Destroy(entity);
                    }
                }
            }
        }
    }


    public Dictionary<float, LevelLayer> LevelLayers = new(); // FIXME: Load from level data
    

    public float OpenedLayerDepth = float.NaN;
    public float SelectedLayerDepth = float.NaN;
    public bool IsActiveLayerTiled => LevelLayers[OpenedLayerDepth].IsTiled;

    public List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)> GetLayerImagesToPaint()
    {
        if (!LevelEditorManipulator.IsInLevelEditor || float.IsNaN(OpenedLayerDepth)
            || TileLayerMenu.ImagesToPaint == null || ImGui.GetIO().WantCaptureMouse
            )
        {
            return new();
        }
        var result = new List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)>();

        var openedLayer = LevelLayers[OpenedLayerDepth];
        var mouseWorldPos = Input.WorldMousePosition;
        var maybeHoveredOverTile = TileManipulator.GetTilePos(mouseWorldPos);
        if (openedLayer.IsTiled && !maybeHoveredOverTile.HasValue)
        {
            return new();
        }

        var startingTile = maybeHoveredOverTile.Value;
        var nextTile = startingTile;
        int column = 0;
        foreach (var (layerImageID, isNotFiller) in TileLayerMenu.ImagesToPaint.LayerImageIDs)
        {
            Position2D worldPos = mouseWorldPos;

            var currentTile = nextTile;

            if (openedLayer.IsTiled)
            {
                nextTile.X += 1;
                column += 1;
                column %= TileLayerMenu.ImagesToPaint.NumColumns;
                if (column == 0)
                {
                    nextTile.X = startingTile.X;
                    nextTile.Y += 1;
                }

                if (!TileManipulator.IsTilePosValid(currentTile))
                {
                    continue;
                }

                worldPos = TileManipulator.TilePosToWorldPos_Centered(currentTile);

                // Tile may be invalid here, for odd selection schemes.
                // Ex: picking 2 sprites that are diagonal from each other.
                // This would produce a 2x2 selection scheme, with 2 tiles being 'invalid' (empty).
                // Or, the tile may actually be an empty filler tile, with a valid LayerImageID.
                if (!isNotFiller || IsEmptyTile(openedLayer.Images[layerImageID].Item1))
                {
                    continue;
                }
            }

            var (sprite, imageColor) = openedLayer.Images[layerImageID];
            imageColor = openedLayer.MixLayerColorWithImageColor(imageColor);
            result.Add((sprite, imageColor, worldPos, new Editor_LayerImageID(layerImageID)));
        }

        if (result.Count > 1 && !openedLayer.IsTiled)
        {
            throw new Exception("Should only be creating 1 image here...");
        }

        return result;
    }

    static bool IsEmptyTile(SpriteAnimation sprite)
    {
        return sprite.SpriteAnimationInfoID == SpriteAnimations.EditorTile_EmptyTile.ID
            || sprite.CurrentSprite.UV == SpriteAnimations.EditorTile_EmptyTile.Frames[0].UV;
    }
    static TileLayerMenu TileLayerMenuStatic = new();

    public float HoveredOverLayerDepth = float.NaN;
    public LevelLayer HoveredOverLayer =>
        float.IsNaN(HoveredOverLayerDepth) ? null : LevelLayers[HoveredOverLayerDepth];

    void ShowLevelLayerOptions(Entity debugEntity)
    {
        HoveredOverLayerDepth = float.NaN;

        if (ImGui.Begin("Level Layers"))
        {
            foreach (var (depth, layer) in LevelLayers)
            {
                var isVisible = layer.IsVisible;
                if (ImGui.Checkbox("##" + layer.Name + "Visibility", ref isVisible))
                {
                    layer.ToggleVisibility(World, debugEntity);
                }
                ImGui.SameLine();

                if (ImGui.Selectable(layer.Name, SelectedLayerDepth == depth, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    SelectedLayerDepth = depth;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        OpenedLayerDepth = depth;
                        TileLayerMenu.ImagesToPaint = null;
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    HoveredOverLayerDepth = depth;
                }

                ImGui.SameLine();
                ImGui.TextColored(Color.Green.ToVector4(),
                    $"\tDepth: {depth}" + (layer.IsDepthLocked ? " (Locked)" : ""));

                if (ImGui.BeginPopup($"RenameLayer{depth}"))
                {
                    string newName = layer.Name;
                    for (int c = newName.Length - 1; c >= 0; --c)
                    {
                        if (char.IsAsciiDigit(newName[c]) || char.IsWhiteSpace(newName[c]))
                        {
                            newName = newName.Remove(c, 1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    // FIXME: Would be nice if we could prevent numbers from being input here...
                    if (ImGui.InputText("##RenameLayerText", ref newName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        LevelLayer.LevelLayerNames.Remove(layer.Name);
                        LevelLayer.ValidateLayerName(ref newName);
                        layer.Name = newName;
                    }
                    ImGui.EndPopup();
                }
            }

            if (ImGui.Button("New"))
            {
                ImGui.OpenPopup("ChooseLayerType");
            }
            if (ImGui.BeginPopup("ChooseLayerType"))
            {
                ImGui.SeparatorText("Layer Type");
                for (int i = 0; i < (int)LevelLayer.LevelLayerTypes.SELECTABLE_COUNT; ++i)
                {
                    var layerType = (LevelLayer.LevelLayerTypes)i;
                    var layerTypeStr = LevelLayer.LayerTypeToString(layerType);
                    if (ImGui.Selectable(layerTypeStr))
                    {
                        var newLayerDepth = (float)DepthLayer.DefaultDepth;
                        if (layerType == LevelLayer.LevelLayerTypes.SolidTile)
                        {
                            newLayerDepth = (float)DepthLayer.Tile_Solid;
                        }

                        LevelLayers.Add(newLayerDepth, new LevelLayer(layerType, layerTypeStr + " Layer"));
                    }
                }
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            bool disabled = false;
            if (float.IsNaN(SelectedLayerDepth))
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Delete"))
            {
                if (OpenedLayerDepth == SelectedLayerDepth)
                {
                    OpenedLayerDepth = float.NaN;
                    TileLayerMenu.ImagesToPaint = null;
                }

                if (HoveredOverLayerDepth == SelectedLayerDepth)
                {
                    HoveredOverLayerDepth = float.NaN;
                }

                // Deleting a layer deletes all entities in it.
                LevelLayer.DeleteLayerCleanup(SelectedLayerDepth, LevelLayers, World);
                SelectedLayerDepth = float.NaN;
            }

            ImGui.SameLine();
            if (ImGui.Button("Rename"))
            {
                ImGui.OpenPopup($"RenameLayer{SelectedLayerDepth}");
            }

            ImGui.SameLine();
            if (!float.IsNaN(SelectedLayerDepth) && LevelLayers[SelectedLayerDepth].IsDepthLocked)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Change Depth"))
            {
                ImGui.OpenPopup($"ChangeLayerDepth");
            }
            if (ImGui.BeginPopup($"ChangeLayerDepth"))
            {
                var oldDepth = SelectedLayerDepth;
                var newDepth = oldDepth;
                if (ImGui.InputFloat("Depth", ref newDepth))
                {
                    if (!LevelLayers.ContainsKey(newDepth))
                    {
                        LevelLayer.ChangeLayerDepth(oldDepth, newDepth, LevelLayers, World);
                    }
                    else
                    {
                        // TODO: Open an explanatory popup
                    }
                }
                ImGui.EndPopup();
            }
            if (disabled)
            {
                ImGui.EndDisabled();
            }
        }
        ImGui.End();


        // Draw separate window to show the active layer options.
        if (!float.IsNaN(OpenedLayerDepth))
        {
            var layer = LevelLayers[OpenedLayerDepth];

            bool stayOpen = true;
            if (ImGui.Begin(layer.Name, ref stayOpen))
            {
                switch (layer.LayerType)
                {
                    case LevelLayer.LevelLayerTypes.SolidTile:
                    case LevelLayer.LevelLayerTypes.VisualTile:
                        TileLayerMenuStatic.Show(layer, World);
                        break;
                    default:
                        // TODO: 
                        break;
                }
            }
            ImGui.End();
            if (!stayOpen)
            {
                OpenedLayerDepth = float.NaN;
            }
        }
    }
};