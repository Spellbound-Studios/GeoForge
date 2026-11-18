// Copyright (c) Spellbound Studios. All rights reserved.

using System.Globalization;
using Spellbound.Core.Authoring;
using Spellbound.Core.Authoring.Editor;
using Spellbound.Core.Operations;
using Spellbound.GeoForge.EditMode;
using Spellbound.Places;
using Spellbound.Places.Owners;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    public static class GeoForgeLayerEditOperation {
        private const string LayerEditDescription =
            "Applies one stroke to a voxel layer of a revised place in the drafts folder, with no scene.";

        [Operation("layer-edit", LayerEditDescription, OperationEffect.Writes)]
        public static OperationResult LayerEdit(OperationContext context,
            [Argument("The revised place in the drafts folder, as asset:<path> or asset:<name>.")]
            PlaceDefinition target,
            [Argument("What the stroke does: add, remove, smooth or paint.")] VoxelStrokeMode mode,
            [Argument("The stroke's shape: sphere or box.")] VoxelStrokeShape shape,
            [Argument("The stroke's centre on x, in metres from the place's origin.")] float x,
            [Argument("The stroke's centre on y, in metres from the place's origin.")] float y,
            [Argument("The stroke's centre on z, in metres from the place's origin.")] float z,
            [Argument("The sphere's diameter or the box's edge, in metres.")] float size,
            [Argument("The material an add or paint stroke writes.")] int material = 0,
            [Argument("The voxel layer's name.")] string layerName = "voxels") {
            var targetPath = AssetDatabase.GetAssetPath(target);
            var draftsFolder = DefinitionDrafts.Folder(context.Host.Settings);

            if (!DefinitionDrafts.IsDraft(target, context.Host.Settings)) {
                return OperationResult.Invalid(targetPath + " is not in the drafts folder " + draftsFolder
                    + "; run core revise on it and edit the revision");
            }

            if (string.IsNullOrWhiteSpace(layerName)) {
                return OperationResult.Invalid("--layer-name is blank");
            }

            if (!(target.FindLayer(layerName) is VoxelLayer layer)) {
                return OperationResult.Invalid(target.name + " holds no " + nameof(VoxelLayer) + " named '"
                    + layerName + "'");
            }

            if (size <= 0f) {
                return OperationResult.Invalid("--size is " + Format(size) + ", a positive size is required");
            }

            if (material < byte.MinValue || material > byte.MaxValue) {
                return OperationResult.Invalid("--material is " + material.ToString(CultureInfo.InvariantCulture)
                    + ", 0 to 255 is required");
            }

            var stroke = new VoxelStroke(shape, mode, new Vector3(x, y, z), size, (byte)material);
            var strokeText = Describe(stroke);
            var runsBefore = RunCount(layer);

            if (context.DryRun) {
                return OperationResult.Previewed("would apply " + strokeText + " to layer " + layerName + " of "
                    + targetPath + ", " + runsBefore.ToString(CultureInfo.InvariantCulture) + " runs before");
            }

            var report = new ValidationReport("geoforge layer-edit");
            var region = new VoxelRegion(Vector3Int.zero, layer.ChunkExtent);
            VoxelLayer captured;

            using (var scratch = ScratchVolume.Create(layer.ChunkSize, EditTerrainSession.DefaultVoxelSize,
                       layer.ChunkExtent, false)) {
                PlaceFirstVoxelAtThePlaceOrigin(scratch.Volume);
                new VoxelLayerOwner(scratch.Volume, region).Stamp(layer, new PlaceStampContext(report));

                if (report.HasErrors) {
                    return OperationResult.Failed("layer " + layerName + " of " + targetPath + " is not stamped: "
                        + report.ToText());
                }

                scratch.Complete();

                using (var brush = new VoxelBrush(scratch.Volume, region)) {
                    if (!brush.Apply(stroke)) {
                        return OperationResult.Failed(strokeText + " was not applied to layer " + layerName + " of "
                            + targetPath);
                    }
                }

                scratch.Complete();
                captured = GeoForgePlacesKit.Capture(scratch.Volume, region, layerName, report);
            }

            if (captured == null || report.HasErrors) {
                return OperationResult.Failed("layer " + layerName + " of " + targetPath + " is not captured: "
                    + report.ToText());
            }

            var store = OwnStore(layer, VoxelRunStores.PathBeside(targetPath, layerName));
            VoxelRunStores.Write(target, layer, captured);
            EditorUtility.SetDirty(store);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            return OperationResult.Success("layer " + layerName + " of " + targetPath + ": " + strokeText + ", "
                + runsBefore.ToString(CultureInfo.InvariantCulture) + " runs before, "
                + RunCount(layer).ToString(CultureInfo.InvariantCulture) + " after, written to "
                + AssetDatabase.GetAssetPath(store));
        }

        private static VoxelRunStore OwnStore(VoxelLayer layer, string storePath) {
            if (layer.Store != null && AssetDatabase.GetAssetPath(layer.Store) == storePath) {
                return layer.Store;
            }

            var store = AssetDatabase.LoadAssetAtPath<VoxelRunStore>(storePath);

            if (store == null) {
                store = ScriptableObject.CreateInstance<VoxelRunStore>();
                store.SetChunks(layer.Chunks);
                AssetDatabase.CreateAsset(store, storePath);
            } else {
                store.SetChunks(layer.Chunks);
            }

            layer.UseStore(store);

            return store;
        }

        private static void PlaceFirstVoxelAtThePlaceOrigin(IGeoVolume volume) {
            var engine = volume.GeoVolumeEngine;
            ref var config = ref engine.ConfigBlob.Value;
            engine.Transform.position = -(Vector3)config.Offset * config.Resolution;
        }

        private static string Describe(in VoxelStroke stroke) {
            var centre = stroke.Centre;
            var text = stroke.Mode.ToString().ToLowerInvariant() + " " + stroke.Shape.ToString().ToLowerInvariant()
                + " of " + Format(stroke.Size) + " m at (" + Format(centre.x) + ", " + Format(centre.y) + ", "
                + Format(centre.z) + ")";

            if (stroke.Mode == VoxelStrokeMode.Add || stroke.Mode == VoxelStrokeMode.Paint) {
                text += " with material " + stroke.Material.ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }

        private static string Format(float value) {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int RunCount(VoxelLayer layer) {
            var count = 0;

            foreach (var chunk in layer.Chunks) {
                count += chunk.Runs.Count;
            }

            return count;
        }
    }
}
