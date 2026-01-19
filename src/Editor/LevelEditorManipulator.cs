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
using RollAndCash.Utility;

namespace RollAndCash.Editor;

public class LevelEditorManipulator : MoonTools.ECS.Manipulator
{
    EditorSystem EditorSystem;
    TileManipulator TileManipulator;
    PrefabManipulator PrefabManipulator;

    public LevelEditorManipulator(
        World world, 
        EditorSystem editorSystem
        ) : base(world)
    {
        EditorSystem = editorSystem;
        TileManipulator = new(World);
        PrefabManipulator = new(World);
    }

    public static bool IsInLevelEditor = false;

    public LoadedLevel ActiveLevel = new()
    {
        ID = new LevelID(LevelList.TestLevel)
    };
    public LoadedLevel.Room? ActiveRoom = null;
    //static bool SnapToGrid = true;
    public Vector2? HoveredOverTilePosition = null;

    private PrefabType PrefabToSpawn = PrefabType.None;

    // To write outside of bin/Debug, in order to get to .csproj location. For editor use only; never ship this!
    // FIXME: This may break on you if you have a different deployment structure!
    /*private static string OptimizedLevelContentPath =
        Path.Combine(@"../../../", Path.Combine("Content", "Levels"))
    ;*/
    private static string EditorLevelContentPath =
        Path.Combine(@"../../../", Path.Combine("EditorContent", "Levels"))
    ;

    // Layout inspired by Elias Daler's tutorial series: 
    // https://edw.is/using-imgui-with-sfml-pt1/
    // MARK: Main Window
    void DrawLevelEditorMainWindow()
    {
        bool stillOpened = IsInLevelEditor;
        if (ImGui.Begin("Level Editor"u8, ref stillOpened))
        {
            ImGui.InputText("Name"u8, ref ActiveLevel.PlayerFacingName, 256);

            if (ActiveLevel.PlayerFacingName == null || ActiveLevel.PlayerFacingName.Length == 0)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("Save"u8))
            {
                // TODO: Create backups of previous level file if possible!

                LevelSerialization.Editor_SaveLevelToFile(
                    ActiveLevel, 
                    EditorLevelContentPath, 
                    World, 
                    PrefabManipulator
                );
            }
            if (ActiveLevel.PlayerFacingName == null || ActiveLevel.PlayerFacingName.Length == 0)
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
                // FIXME: Unload everything from the current level first!!!
            }

            if (ImGui.BeginPopup("##LoadLevelPopup"u8))
            {
                foreach (var levelPathStr 
                    in Directory.GetFiles(EditorLevelContentPath))
                {
                    if (ImGui.Button(levelPathStr))
                    {
                        // FIXME: Unload everything from the current level first!!!
                        ActiveLevel = LevelSerialization.LoadLevelFromFile(
                            levelPathStr, 
                            World, 
                            PrefabManipulator
                        );
                    }
                }
                ImGui.EndPopup();
            }

            // TODO: Snap to grid option? Not sure if I should support going off-grid yet.
            //ImGui.Checkbox("Snap to Grid", ref SnapEntitiesToGrid);
        }
        ImGui.End();

        if (!stillOpened)
        {
            IsInLevelEditor = false;
        }
    }

    private void SetOrRevertPaintingTool()
    {
        var hasSelectedPrefab = PrefabToSpawn != PrefabType.None;
        if (VisualSetMenu.SelectedToPaint.TrueSelections.Count != 0
            || hasSelectedPrefab)
        {
            EditorSystem.ActiveTool.TrySetMode(ToolMode.PaintingAndErasing);
        }
        else if (EditorSystem.ActiveTool.CurrentMode == ToolMode.PaintingAndErasing)
        {
            EditorSystem.ActiveTool.RevertToDefaultMode();
        }
    }

    private bool IsDragDeleting = false;
    private bool IsDragCreating = false;

    // MARK: Main Code
    public void HandleLevelEditor(Entity debugEntity)
    {
        void ShowWarningIfSelectedVisualsToPaintAndCannotPaint()
        {
            if (HasSelectedVisualsToPaint)
            {
                ImGui.GetBackgroundDrawList().AddText(ImGui.GetMousePos(), 0xFFFFFFFF, 
                    "Cannot Paint: Must be in Level Editor, with a Level + Room loaded, and a layer menu open."u8
                );
            }
        }

        SetOrRevertPaintingTool();

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
            ShowWarningIfSelectedVisualsToPaintAndCannotPaint();
            return;
        }

        DrawLevelEditorMainWindow();

        if (ActiveLevel == null || ActiveRoom == null)
        {
            ShowWarningIfSelectedVisualsToPaintAndCannotPaint();
            return;
        }

        PrefabManipulator.ShowPrefabSpawnerAndMaybeSpawn(
            ref PrefabToSpawn,
            debugEntity
        );
        var hasSelectedPrefab = PrefabToSpawn != PrefabType.None;
        // Update painting tool status w/ potential PrefabToSpawn change.
        SetOrRevertPaintingTool();

        ShowActiveLayersForRoom_Menu(debugEntity);

        //== Layer painting controls.
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
        if (MenuOpenedLayer == null || MenuOpenedLayer.MaybeVisualSet == null)
        {
            ShowWarningIfSelectedVisualsToPaintAndCannotPaint();
            return;
        }
        if (!MenuOpenedLayer.MaybeVisualSetVariantID.HasValue)
        {
            ShowWarningIfSelectedVisualsToPaintAndCannotPaint();
            Logger.LogError("VisualSetVariantID should not be null here!");
            return;
        }
        if (EditorSystem.ActiveTool.CurrentMode != ToolMode.PaintingAndErasing)
        {
            return;
        }

        var visualsToPaint = GetSelectedVisualsToPaint();

        // Spawning for prefabs was already handled above, in prefab menu code.
        if (!hasSelectedPrefab && visualsToPaint.Count >= 1 
            && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            PaintVisuals(visualsToPaint);
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            // Erase/delete entities on this level layer!
            if (MenuOpenedLayer.IsTiled || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (MenuOpenedLayer.IsTiled && !IsDragDeleting)
                {
                    IsDragDeleting = true;
                    UndoRedo.BeginGroupedChange();
                }

                var mouseHitboxRect = new Rectangle(0, 0, 1, 1);
                var mouseWorldPosRect = mouseHitboxRect.GetWorldRect(mouseWorldPos);

                foreach (var entity in MenuOpenedLayer.CachedEntities)
                {
                    var rect = EditorSystem.GetEntityVisualRect(entity)!;
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

    public LoadedLevel.EditorLayer? MenuOpenedLayer = null;
    public LoadedLevel.EditorLayer? SelectedLayerInList = null;
    public LoadedLevel.EditorLayer? HoveredOverLayer = null;

    // MARK: Get Entities To Paint
    public bool HasSelectedVisualsToPaint => 
        VisualSetMenu.SelectedToPaint.Selected.Count != 0;

    public List<(VisualFromSetID_ForSpawning, Position2D)> GetSelectedVisualsToPaint()
    {
        if (!IsInLevelEditor
            || ActiveLevel == null
            || MenuOpenedLayer == null
            || MenuOpenedLayer.MaybeVisualSet == null
            || VisualSetMenu.SelectedToPaint.Selected.Count == 0
            || ImGui.GetIO().WantCaptureMouse
            )
        {
            return new();
        }

        if (!MenuOpenedLayer.MaybeVisualSetVariantID.HasValue)
        {
            Logger.LogError("VisualSetVariantID should not be null here!");
            return new();
        }

        // A VisualSet-type layer has only 1 constant VisualSet variant it can draw from. 
        var activeVisualMenu = VisualSetMenu.ActiveVisualSetMenu!;
        if (activeVisualMenu.VisualSet.ID != MenuOpenedLayer.MaybeVisualSet.ID
            || activeVisualMenu.CurrentVariantID != MenuOpenedLayer.MaybeVisualSetVariantID.Value)
        {
            return new();
        }

        var result = new List<(VisualFromSetID_ForSpawning, Position2D)>();
        var activeVisualSetMenu = VisualSetMenu.ActiveVisualSetMenu;
        var mouseWorldPos = Input.WorldMousePosition;
        var maybeHoveredOverTile = TileManipulator.GetTilePos(mouseWorldPos);

        if (MenuOpenedLayer.IsTiled)
        {
            if (!maybeHoveredOverTile.HasValue)
            {
                return new();
            }

            var startingTile = maybeHoveredOverTile.Value;
            var nextTile = startingTile;
            int column = 0;
            foreach (var (posInVisualSet, isNotFiller) in VisualSetMenu.SelectedToPaint.Selected)
            {
                Position2D worldPos = mouseWorldPos;

                var currentTile = nextTile;

                if (MenuOpenedLayer.IsTiled)
                {
                    nextTile.X += 1;
                    column += 1;
                    column %= VisualSetMenu.SelectedToPaint.NumColumns;
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
                    if (!isNotFiller /*|| IsEmptyTile(tileSet[posInVisualSet].Item1)*/)
                    {
                        continue;
                    }
                }

                var visualFromSetID = new VisualFromSetID_ForSpawning(
                    posInVisualSet, 
                    activeVisualMenu.VisualSet.ID,
                    activeVisualMenu.CurrentVariantID
                );

                result.Add((visualFromSetID, worldPos));
            }
        }
        else
        {
            Logger.LogError("Unimplemented!");
            // FIXME: TODO: Implement for other VisualSets, like ImageSets!
        }

        if (result.Count > 1 && !MenuOpenedLayer.IsTiled)
        {
            Logger.LogError("Should only be creating 1 image here...");
        }

        return result;
    }

    //FIXME: This trick no longer works!
    static bool IsEmptyTile(SpriteAnimation sprite)
    {
        return sprite.SpriteAnimationInfoID == SpriteAnimations.EditorTile_EmptyTile.ID
            || sprite.CurrentSprite.UV == SpriteAnimations.EditorTile_EmptyTile.Frames[0].UV;
    }

    // MARK: Paint Entities
    /// <summary>
    /// Assumes MenuOpenedLayer isn't null.
    /// </summary>
    /// <param name="visualsToPaint"></param>
    /// <returns></returns>
    private List<Entity> PaintVisuals(
        List<(VisualFromSetID_ForSpawning, Position2D)> visualsToPaint
        )
    {
        List<Entity> paintedEntities = new();
        List<Entity> deletedEntities = new();

        switch (MenuOpenedLayer!.LayerType)
        {
            case LevelLayerTypes.TileSet:
                if (visualsToPaint.Count == 1 || ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    if (visualsToPaint.Count == 1 && !IsDragCreating)
                    {
                        IsDragCreating = true;
                        UndoRedo.BeginGroupedChange();
                    }

                    foreach (var (visualFromSetID, posInWorld) in visualsToPaint)
                    {
                        bool spawn = true;
                        foreach (var entity in MenuOpenedLayer.CachedEntities)
                        {
                            if (Get<Position2D>(entity) == posInWorld)
                            {
                                // Currently, a layer should have at most 1 VisualSet variant selected,
                                // so PosInSet is all that needs to be checked.
                                // I'll keep it more exhaustive in case this changes later.
                                if (Get<TileID>(entity) != (TileID)visualFromSetID)
                                {
                                    // Replace a tile if one is already painted in at this level layer.
                                    deletedEntities.Add(entity);
                                }
                                else
                                {
                                    // No point in replacing ourselves.
                                    spawn = false;
                                }
                                // Assume there can't be any other entities on this layer at this tile pos.
                                break;
                            }
                        }

                        if (spawn)
                        {
                            var maybeNewEntity = VisualSet.Editor_TryCreateEntityFromVisualSet(
                                visualFromSetID,
                                posInWorld,
                                World,
                                PrefabManipulator,
                                ActiveRoom,
                                MenuOpenedLayer,
                                false,
                                false
                            );
                            
                            if (maybeNewEntity.HasValue)
                            {
                                paintedEntities.Add(maybeNewEntity.Value);
                            }
                        }
                    }
                }
                break;
            /*case LevelLayerTypes.ImageSet:
            
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    var (spriteAnim, color, position, layerImageID) = visualsToPaint[0];

                    Entity newEntity = CreateEntity("Image");
                    Set(newEntity, new PrefabID(Prefabs.Image));
                    Set(newEntity, spriteAnim);
                    Set(newEntity, new ColorBlend(color));
                    Set(newEntity, position);
                    Set(newEntity, layerImageID);
                }
                break;
                */
        }

        if (paintedEntities.Count != 0)
        {
            if (!IsDragCreating)
            {
                // Group together multiple entities created in a single paintbrush stroke for Undo.
                UndoRedo.BeginGroupedChange();
            }

            foreach (var entity in paintedEntities)
            {
                Set(entity, new Depth(MenuOpenedLayer.Depth));
                Set(entity, MenuOpenedLayer.LayerID);

                UndoRedo.RememberEntityCreation(entity, World);
            }

            foreach (var entity in deletedEntities)
            {
                UndoRedo.RememberEntityDestruction(entity, World);
            }

            if (!IsDragCreating)
            {
                UndoRedo.EndGroupedChange();
            }
        }

        return paintedEntities;
    }

    // MARK: Show Layers
    LevelLayerTypes NewLayerType;

    void ShowActiveLayersForRoom_Menu(Entity debugEntity)
    {
        HoveredOverLayer = null;

        if (ImGui.Begin("Room Layers"u8))
        {
            foreach (var layer in ActiveRoom!.Layers)
            {
                var isVisible = layer.IsVisible;
                if (ImGui.Checkbox("##" + layer.Name + "Visibility", ref isVisible))
                {
                    layer.ToggleVisibility(World, debugEntity);
                }
                ImGui.SameLine();

                if (ImGui.Selectable(layer.Name, SelectedLayerInList == layer, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        MenuOpenedLayer = layer;
                        VisualSetMenu.SelectedToPaint = new();
                    }
                    else if (SelectedLayerInList == layer)
                    {
                        SelectedLayerInList = null;
                    }
                    else
                    {
                        SelectedLayerInList = layer;
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    HoveredOverLayer = layer;
                }

                ImGui.SameLine();
                ImGui.TextColored(Color.Green.ToVector4(),
                    $"\tDepth: {layer.Depth}" + (layer.IsDepthLocked ? " (Locked)" : ""));

                if (ImGui.BeginPopup($"RenameLayer{layer.Name}"))
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
                    // TODO: Might be nice if we could prevent numbers from being input here
                    if (ImGui.InputText("##RenameLayerText", ref newName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        if (!layer.IsNameLocked)
                        {
                            ActiveRoom.ValidateLayerName(ref newName);
                            layer.TrySetName(newName);
                        }
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
                    var layerTypeStr = LoadedLevel.EditorLayer.LayerTypeToString(layerType);
                    if (ImGui.Selectable(layerTypeStr))
                    {
                        if (LevelLayerTypesFuncs.IsVisualSet(layerType))
                        {
                            NewLayerType = layerType;
                            ImGui.BeginPopup("ChooseLayerVisualSet"u8);
                        }
                        else
                        {
                            var _ = new LoadedLevel.EditorLayer(layerType, ActiveRoom, layerTypeStr);
                        }
                    }
                }
                ImGui.EndPopup();
            }
            if (ImGui.BeginPopup("ChooseLayerVisualSet"u8))
            {
                ImGui.SeparatorText("Select a Visual Set + VariantID"u8);

                // TODO: Add option to select a VisualSet type, then filter by name.

                if (VisualSetMenu.ActiveVisualSetMenu != null)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button("Use currently active Visual Set + VariantID"))
                {
                    var layerTypeStr = LoadedLevel.EditorLayer.LayerTypeToString(NewLayerType);
                    var newLayer = new LoadedLevel.EditorLayer(NewLayerType, ActiveRoom, layerTypeStr);
                    newLayer.MaybeVisualSet = VisualSetMenu.ActiveVisualSetMenu!.VisualSet;
                    newLayer.MaybeVisualSetVariantID = VisualSetMenu.ActiveVisualSetMenu.CurrentVariantID;
                }
                if (VisualSetMenu.ActiveVisualSetMenu != null)
                {
                    ImGui.EndDisabled();
                }
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            bool disabled = false;
            if (SelectedLayerInList == null)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Delete"u8))
            {
                if (MenuOpenedLayer == SelectedLayerInList)
                {
                    MenuOpenedLayer = null;
                }
                if (HoveredOverLayer == SelectedLayerInList)
                {
                    HoveredOverLayer = null;
                }

                // Deleting a layer deletes all entities in it.
                ActiveRoom.DeleteLayerCleanup(ref SelectedLayerInList, World);
            }

            ImGui.SameLine();
            if (ImGui.Button("Rename"u8))
            {
                ImGui.OpenPopup($"RenameLayer{SelectedLayerInList!.Name}");
            }

            ImGui.SameLine();
            if (SelectedLayerInList != null && SelectedLayerInList.IsDepthLocked)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Change Depth"u8))
            {
                ImGui.OpenPopup($"ChangeLayerDepth{SelectedLayerInList!.Name}");
            }
            if (ImGui.BeginPopup($"ChangeLayerDepth{SelectedLayerInList!.Name}"))
            {
                var selectedLayer = SelectedLayerInList;
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


        // Draw separate window to show the active layer info.
        if (MenuOpenedLayer != null)
        {
            var layer = MenuOpenedLayer;

            bool stayOpen = true;
            if (ImGui.Begin(layer.Name, ref stayOpen))
            {
                ImGui.Text($"Layer type: {layer.LayerType}");

                if (layer.LayerType == LevelLayerTypes.Prefabs)
                {
                    ImGui.Text($"Prefab type: {layer.MaybePrefabType!.Value}");
                }
                else 
                {
                    ImGui.Text($"Visual set: {layer.MaybeVisualSet!.Name}. VariantID: {layer.MaybeVisualSetVariantID!.Value}");
                }
                ImGui.Text("Hint: create a new layer if you want to change the above, or manually edit the saved file.");

                if (ImGui.Button("Toggle Color Blend"))
                {
                    if (layer.MaybeColor.HasValue)
                    {
                        layer.ChangeLayerColorBlend(
                            new Toggleable<Color>(
                                layer.MaybeColor.Value_Unsafe, false
                            ), 
                            World
                        );
                    }
                    else
                    {
                        layer.ChangeLayerColorBlend(Color.White, World);
                    }
                }
                if (layer.MaybeColor.HasValue)
                {
                    var colorVec = layer.MaybeColor.Value.ToVector4();
                    if (ImGui.ColorEdit4("ColorBlend", ref colorVec))
                    {
                        layer.ChangeLayerColorBlend(new Color(colorVec), World);
                    }
                }
            }
            ImGui.End();
            if (!stayOpen)
            {
                MenuOpenedLayer = null;
            }
        }
    }
};
#endif