// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Globalization;
using Spellbound.Core.Operations;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    public static class GeoForgeBakeOperation {
        private const string BakeDescription = "Fills a voxel layer of a place from a GeoForge density source over "
                                               + "the layer's region and writes a report beside the asset.";

        [Operation("bake", BakeDescription, OperationEffect.Writes)]
        public static OperationResult Bake(OperationContext context,
            [Argument("The place, as asset:<path> or asset:<name>.")] PlaceDefinition target,
            [Argument("The data generator asset the density is read from.")] GeoForgeDataGenerator generator,
            [Argument("The voxel volume config asset giving chunk size and voxel size.")] VoxelVolumeConfig config,
            [Argument("The voxel layer's name; added when absent.")] string layerName = "voxels",
            [Argument("The seed given to the density source.")] int seed = 0) {
            if (string.IsNullOrWhiteSpace(layerName)) {
                return OperationResult.Invalid("--layer-name is not blank");
            }

            var found = target.FindLayer(layerName);

            if (found != null && !(found is VoxelLayer)) {
                return OperationResult.Invalid(target.name + " holds layer '" + layerName + "' as "
                    + found.GetType().Name + ", not " + nameof(VoxelLayer));
            }

            var region = found is VoxelLayer foundLayer
                ? new VoxelRegion(Vector3Int.zero, foundLayer.ChunkExtent)
                : VoxelRegion.Origin;
            VoxelLayer baked;

            try {
                baked = VoxelLayerBaker.Bake(generator, config, region, layerName, unchecked((uint)seed));
            } catch (ArgumentException exception) {
                return OperationResult.Invalid(exception.Message);
            }

            var chunkCount = baked.Chunks.Count.ToString(CultureInfo.InvariantCulture);
            var runCount = RunCount(baked).ToString(CultureInfo.InvariantCulture);
            var assetPath = AssetDatabase.GetAssetPath(target);

            if (context.DryRun) {
                return OperationResult.Previewed("would write " + chunkCount + " chunks, " + runCount
                    + " runs to layer " + layerName + " of " + assetPath + " and its report");
            }

            var written = baked;

            if (found is VoxelLayer existing) {
                existing.SetChunks(baked.ChunkSize, baked.ChunkExtent, baked.Chunks);
                written = existing;
            } else {
                target.AddLayer(baked);
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            var reportPath = BakeReport.Write(assetPath,
                BakeReport.Lines(target, written, generator, config, VoxelLayerBaker.VoxelSize(config)));

            return OperationResult.Success("layer " + layerName + ": " + chunkCount + " chunks, " + runCount
                + " runs; report " + reportPath);
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
