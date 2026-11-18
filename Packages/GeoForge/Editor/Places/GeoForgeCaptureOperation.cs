// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Globalization;
using Spellbound.Core.Authoring;
using Spellbound.Core.Operations;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    public static class GeoForgeCaptureOperation {
        private const string CaptureDescription =
            "Captures a region of the open scene's GeoForge volume into a voxel layer of a place.";

        [Operation("capture", CaptureDescription, OperationEffect.Writes)]
        public static OperationResult Capture(OperationContext context,
            [Argument("The place, as asset:<path> or asset:<name>.")] PlaceDefinition target,
            [Argument("The voxel layer's name; added when absent.")] string layerName = "voxels",
            [Argument("The region's minimum chunk on x.")] int chunkX = 0,
            [Argument("The region's minimum chunk on y.")] int chunkY = 0,
            [Argument("The region's minimum chunk on z.")] int chunkZ = 0,
            [Argument("Chunks per axis of the region.")] int extent = 1) {
            if (string.IsNullOrWhiteSpace(layerName)) {
                return OperationResult.Invalid("--layer-name is blank");
            }

            if (extent < 1) {
                return OperationResult.Invalid("--extent is " + extent.ToString(CultureInfo.InvariantCulture)
                    + ", one or more is required");
            }

            var volume = SceneVolumes.FirstWithEngine();

            if (volume == null) {
                return OperationResult.Invalid("no GeoForge volume with a running engine is in the open scene");
            }

            var found = target.FindLayer(layerName);

            if (found != null && !(found is VoxelLayer)) {
                return OperationResult.Invalid(target.name + " holds layer '" + layerName + "' as "
                    + found.GetType().Name + ", not " + nameof(VoxelLayer));
            }

            var report = new ValidationReport("geoforge capture");
            var region = new VoxelRegion(new Vector3Int(chunkX, chunkY, chunkZ),
                new Vector3Int(extent, extent, extent));
            var captured = GeoForgePlacesKit.Capture(volume, region, layerName, report);

            if (captured == null || report.HasErrors) {
                return OperationResult.Failed("layer " + layerName + " is not captured: " + FindingCodes(report));
            }

            var chunkCount = captured.Chunks.Count.ToString(CultureInfo.InvariantCulture);
            var runCount = RunCount(captured).ToString(CultureInfo.InvariantCulture);
            var assetPath = AssetDatabase.GetAssetPath(target);

            if (context.DryRun) {
                return OperationResult.Previewed("would write " + chunkCount + " chunks, " + runCount
                    + " runs to layer " + layerName + " of " + assetPath);
            }

            VoxelRunStores.Write(target, found as VoxelLayer, captured);

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            return OperationResult.Success("layer " + layerName + ": " + chunkCount + " chunks, " + runCount
                + " runs written to " + assetPath);
        }

        private static int RunCount(VoxelLayer layer) {
            var count = 0;

            foreach (var chunk in layer.Chunks) {
                count += chunk.Runs.Count;
            }

            return count;
        }

        private static string FindingCodes(ValidationReport report) {
            var codes = new List<string>();

            foreach (var finding in report.Findings) {
                codes.Add(finding.Code);
            }

            return codes.Count == 0 ? "no layer was returned" : string.Join(", ", codes);
        }
    }
}
