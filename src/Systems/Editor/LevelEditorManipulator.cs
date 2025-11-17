#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
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
    public LiveEditorLevel Level = new();
    public bool HasSelectedPrefab = false;
    static bool SnapToGrid = true;
    public static bool ShowGrid = true;
    public Vector2? HoveredOverTilePosition = null;
    public static Vector4 GridLineColor = (Color.DarkTurquoise * 0.5f).ToVector4();

    // To write outside of bin/Debug, in order to get to .csproj location. For editor use only; never ship this!
    // FIXME: This may break on you if you have a different deployment structure!
    private static string OptimizedLevelContentPath =
        Path.Combine(@"../../../", Path.Combine("Content", "Levels"))
    ;
    private static string EditorLevelContentPath =
        Path.Combine(@"../../../", Path.Combine("EditorContent", "Levels"))
    ;

    // Layout inspired by Elias Daler's tutorial series: https://edw.is/using-imgui-with-sfml-pt1/
    void DrawLevelEditorMainWindow()
    {
        bool stillOpened = IsInLevelEditor;
        if (ImGui.Begin("Level Editor"u8, ref stillOpened))
        {
            //FIXME: ImGui.Text("Level path: ");
            //FIXME: ImGui.Text("Camera: ");
            ImGui.Text($"Mouse world position: {Input.WorldMousePosition}");
            ImGui.Text($"Tile position: {TileManipulator.GetTilePos(Input.WorldMousePosition)}");

            ImGui.Checkbox("Show Grid?"u8, ref ShowGrid);
            ImGui.ColorEdit4("Grid Line Color", ref GridLineColor);

            // TODO: Snap to grid option? Not sure if I should support going off-grid yet.
            //ImGui.Checkbox("Snap to Grid", ref SnapEntitiesToGrid);

            ImGui.Separator();
            ImGui.Text("Level Options"u8);

            ImGui.InputText("Name"u8, ref Level.Name, 30);

            if (Level.Name == null || Level.Name.Length == 0)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("Save"u8))
            {
                // TODO: Create backups of previous level file if possible!

                Level.SaveToFile(EditorLevelContentPath, World, PrefabManipulator);
            }
            if (Level.Name == null || Level.Name.Length == 0)
            {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();
            if (ImGui.Button("Load"u8))
            {
                // TODO: add a warning if there's unsaved changes!
                ImGui.OpenPopup("##LoadLevelPopup"u8);
            }
            ImGui.SameLine();
            if (ImGui.Button("New"u8))
            {
                // TODO: add a warning if there's unsaved changes!
            }

            if (ImGui.BeginPopup("##LoadLevelPopup"u8))
            {
                foreach (var levelPathStr in Directory.GetFiles(EditorLevelContentPath))
                {
                    if (ImGui.Button(levelPathStr))
                    {
                        // FIXME: Unload everything from the current level first!!!
                        Level = LiveEditorLevel.LoadFromFile(levelPathStr, World, PrefabManipulator);
                    }
                }
                ImGui.EndPopup();
            }
        }
        ImGui.End();

        if (!stillOpened)
        {
            IsInLevelEditor = false;
        }
    }

    private bool IsDragDeleting = false;
    private bool IsDragCreating = false;

    public void HandleLevelEditor(Entity debugEntity)
    {
        if (IsDragCreating && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            IsDragCreating = false;
            UndoRedo.EndGroupedChange();
        }
        if (IsDragDeleting && !ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            IsDragDeleting = false;
            UndoRedo.EndGroupedChange();
        }

        if (!IsInLevelEditor)
        {
            return;
        }

        HasSelectedPrefab = PrefabManipulator.ShowPrefabSpawner(debugEntity);
        if (HasSelectedPrefab)
        {
            EditorSystem.IsInEntitySelectionMode = false;
        }

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
        if (OpenedLayerName == null)
        {
            return;
        }
        var activeLayer = Level.Layers[OpenedLayerName];
        var imagesToPaint = GetLayerImagesToPaint();

        if (imagesToPaint != null && imagesToPaint.Count >= 1)
        {
            EditorSystem.IsInEntitySelectionMode = false;
        }

        if (!HasSelectedPrefab && imagesToPaint != null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            List<Entity> paintedEntities = new();

            switch (activeLayer.LayerType)
            {
                case LevelLayerTypes.VisualTileSet:
                case LevelLayerTypes.SolidTileSet:
                    if (imagesToPaint.Count == 1 || ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (imagesToPaint.Count == 1 && !IsDragCreating)
                        {
                            IsDragCreating = true;
                            UndoRedo.BeginGroupedChange(UndoRedo.ChangeType.Entity_Creation);
                        }

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
                                if (activeLayer.LayerType == LevelLayerTypes.SolidTileSet)
                                {
                                    newEntity = TileManipulator.SpawnSolidTile(tileWorldPos, imageSprite);
                                }
                                else
                                {
                                    newEntity = CreateEntity("Visual Tile");
                                    Set(newEntity, new PrefabID(Prefabs.VisualTile));
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
                case LevelLayerTypes.Image:
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        var (spriteAnim, color, position, layerImageID) = imagesToPaint[0];

                        Entity newEntity = CreateEntity("Image");
                        Set(newEntity, new PrefabID(Prefabs.Image));
                        Set(newEntity, spriteAnim);
                        Set(newEntity, new ColorBlend(color));
                        Set(newEntity, position);
                        Set(newEntity, layerImageID);
                    }
                    break;
            }

            if (paintedEntities.Count != 0)
            {
                // Group together multiple entities created in a single paintbrush stroke for Undo.
                UndoRedo.BeginGroupedChange(UndoRedo.ChangeType.Entity_Creation);
                foreach (var entity in paintedEntities)
                {
                    Set(entity, new Depth(activeLayer.Depth));
                    Set(entity, activeLayer.LayerID);

                    UndoRedo.RememberEntityCreation(entity, World);
                }
                UndoRedo.EndGroupedChange();
            }
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && !EditorSystem.IsInEntitySelectionMode)
        {
            // Erase/Delete tiles on this level layer!
            if (activeLayer.IsTiled || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (activeLayer.IsTiled && !IsDragDeleting)
                {
                    IsDragDeleting = true;
                    UndoRedo.BeginGroupedChange(UndoRedo.ChangeType.Entity_Deletion);
                }

                var mouseHitboxRect = new Rectangle(0, 0, 1, 1);
                var mouseWorldPosRect = mouseHitboxRect.GetWorldRect(mouseWorldPos);

                // Hopefully won't need an acceleration structure for this...
                foreach (var entity in activeLayer.CachedEntities)
                {
                    var rect = EditorSystem.GetEntityVisualRect(entity);
                    var worldRect = rect.Value.GetWorldRect(Get<Position2D>(entity));

                    if (worldRect.Intersects(mouseWorldPosRect))
                    {
                        UndoRedo.RememberEntityDestruction(entity, World);
                        Destroy(entity);
                    }
                }
            }
        }
    }

    public string OpenedLayerName = null;
    public string SelectedLayerName = null;
    public bool IsActiveLayerTiled => Level.Layers[OpenedLayerName].IsTiled;

    public List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)> GetLayerImagesToPaint()
    {
        if (!IsInLevelEditor || OpenedLayerName == null
            || TileLayerMenu.ImagesToPaint == null || ImGui.GetIO().WantCaptureMouse
            )
        {
            return new();
        }
        var result = new List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)>();

        var openedLayer = Level.Layers[OpenedLayerName];
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

    public string HoveredOverLayerName = null;
    public LiveEditorLevel.Layer HoveredOverLayer =>
        HoveredOverLayerName == null ? null : Level.Layers[HoveredOverLayerName];

    void ShowLevelLayerOptions(Entity debugEntity)
    {
        HoveredOverLayerName = null;

        if (ImGui.Begin("Level Layers"u8))
        {
            foreach (var (name, layer) in Level.Layers)
            {
                var isVisible = layer.IsVisible;
                if (ImGui.Checkbox("##" + layer.Name + "Visibility", ref isVisible))
                {
                    layer.ToggleVisibility(World, debugEntity);
                }
                ImGui.SameLine();

                if (ImGui.Selectable(layer.Name, SelectedLayerName == name, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        OpenedLayerName = name;
                        TileLayerMenu.ImagesToPaint = null;
                    }
                    else if (SelectedLayerName == name)
                    {
                        SelectedLayerName = null;
                    }
                    else
                    {
                        SelectedLayerName = name;
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    HoveredOverLayerName = name;
                }

                ImGui.SameLine();
                ImGui.TextColored(Color.Green.ToVector4(),
                    $"\tDepth: {layer.Depth}" + (layer.IsDepthLocked ? " (Locked)" : ""));

                if (ImGui.BeginPopup($"RenameLayer{name}"))
                {
                    string oldName = layer.Name;
                    string newName = oldName;
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
                        Level.ValidateLayerName(ref newName);
                        layer.Name = newName;
                        Level.Layers.Add(newName, Level.Layers[oldName]);
                        Level.Layers.Remove(oldName);
                    }
                    ImGui.EndPopup();
                }
            }

            if (ImGui.Button("New"u8))
            {
                ImGui.OpenPopup("ChooseLayerType"u8);
            }
            if (ImGui.BeginPopup("ChooseLayerType"u8))
            {
                ImGui.SeparatorText("Layer Type"u8);
                for (int i = 0; i < (int)LevelLayerTypes.SELECTABLE_IN_EDITOR_MAX; ++i)
                {
                    var layerType = (LevelLayerTypes)i;
                    var layerTypeStr = LiveEditorLevel.Layer.LayerTypeToString(layerType);
                    if (ImGui.Selectable(layerTypeStr))
                    {
                        var newLayerDepth = (float)DepthLayer.DefaultDepth;
                        if (layerType == LevelLayerTypes.SolidTileSet)
                        {
                            newLayerDepth = (float)DepthLayer.SolidObject;
                        }

                        var newLayer = new LiveEditorLevel.Layer(layerType, Level, layerTypeStr, newLayerDepth);
                    }
                }
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            bool disabled = false;
            if (SelectedLayerName == null)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Delete"u8))
            {
                if (OpenedLayerName == SelectedLayerName)
                {
                    OpenedLayerName = null;
                    TileLayerMenu.ImagesToPaint = null;
                }

                if (HoveredOverLayerName == SelectedLayerName)
                {
                    HoveredOverLayerName = null;
                }

                // Deleting a layer deletes all entities in it.
                Level.DeleteLayerCleanup(SelectedLayerName, World);
                SelectedLayerName = null;
            }

            ImGui.SameLine();
            if (ImGui.Button("Rename"u8))
            {
                ImGui.OpenPopup($"RenameLayer{SelectedLayerName}");
            }

            ImGui.SameLine();
            if (SelectedLayerName != null && Level.Layers[SelectedLayerName].IsDepthLocked)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Change Depth"u8))
            {
                ImGui.OpenPopup($"ChangeLayerDepth{SelectedLayerName}");
            }
            if (ImGui.BeginPopup($"ChangeLayerDepth{SelectedLayerName}"))
            {
                var selectedLayer = Level.Layers[SelectedLayerName];
                var newDepth = selectedLayer.Depth;
                if (ImGui.InputFloat("Depth"u8, ref newDepth))
                {
                    selectedLayer.ChangeLayerDepth(newDepth, World);
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
        if (OpenedLayerName != null)
        {
            var layer = Level.Layers[OpenedLayerName];

            bool stayOpen = true;
            if (ImGui.Begin(layer.Name, ref stayOpen))
            {
                switch (layer.LayerType)
                {
                    case LevelLayerTypes.SolidTileSet:
                    case LevelLayerTypes.VisualTileSet:
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
                OpenedLayerName = null;
            }
        }
    }
};
#endif