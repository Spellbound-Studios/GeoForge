// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core.Logging;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    internal static class TerraformPaintCommand {
        internal static bool ExecuteSphere(IGeoVolume geoVolume, Vector3 worldPosition, float size, byte material) {
            if (!CanPaint(geoVolume, material, out var gfManager))
                return false;

            return Execute(
                gfManager, geoVolume, worldPosition, TerraformStrokeRegion.Sphere(geoVolume, worldPosition, size),
                material);
        }

        internal static bool ExecuteCube(IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, byte material) {
            if (!CanPaint(geoVolume, material, out var gfManager))
                return false;

            return Execute(
                gfManager, geoVolume, worldPosition, TerraformStrokeRegion.Cube(geoVolume, worldPosition, halfExtent),
                material);
        }

        private static bool CanPaint(IGeoVolume geoVolume, byte material, out GeoForgeManager gfManager) {
            if (!TerraformCommandUtility.TryGetManager(geoVolume, nameof(TerraformPaintCommand), out gfManager))
                return false;

            if (material < VoxelEncoding.NullMaterialIndex)
                return true;

            Log.Error($"{nameof(TerraformPaintCommand)}: material {material} cannot be painted; " +
                      $"only indices below {VoxelEncoding.NullMaterialIndex} can.");

            return false;
        }

        private static bool Execute(
            GeoForgeManager gfManager, IGeoVolume geoVolume, Vector3 worldPosition, TerraformStrokeRegion region,
            byte material) {
            if (!region.TryValidate(geoVolume, gfManager, nameof(TerraformPaintCommand), worldPosition))
                return false;

            geoVolume.OnTerraform(worldPosition, region.BoundingVoxelCount);

            var voxels = new NativeList<int3>(region.BoundingVoxelCount, Allocator.TempJob);
            var deltas = new NativeList<short>(region.BoundingVoxelCount, Allocator.TempJob);

            try {
                region.CollectVoxels(voxels);
                deltas.Resize(voxels.Length, NativeArrayOptions.ClearMemory);

                TerraformCommandUtility.DispatchVoxelDeltas(
                    gfManager, geoVolume, worldPosition, voxels, deltas, VoxelEditKind.MaterialPaint, material,
                    nameof(TerraformPaintCommand));
            }
            finally {
                voxels.Dispose();
                deltas.Dispose();
            }

            return true;
        }
    }
}
