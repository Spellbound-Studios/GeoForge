// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    internal static class TerraformSmoothCommand {
        private const int NeighbourhoodRadius = 1;
        private const float NeighbourhoodVoxelCount = 27f;

        internal static bool ExecuteSphere(IGeoVolume geoVolume, Vector3 worldPosition, float size) {
            if (!TerraformCommandUtility.TryGetManager(geoVolume, nameof(TerraformSmoothCommand), out var manager))
                return false;

            return Execute(
                manager, geoVolume, worldPosition, TerraformStrokeRegion.Sphere(geoVolume, worldPosition, size));
        }

        internal static bool ExecuteCube(IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent) {
            if (!TerraformCommandUtility.TryGetManager(geoVolume, nameof(TerraformSmoothCommand), out var manager))
                return false;

            return Execute(
                manager, geoVolume, worldPosition, TerraformStrokeRegion.Cube(geoVolume, worldPosition, halfExtent));
        }

        private static bool Execute(
            GeoForgeManager manager, IGeoVolume geoVolume, Vector3 worldPosition, TerraformStrokeRegion region) {
            if (!region.TryValidate(geoVolume, manager, nameof(TerraformSmoothCommand), worldPosition))
                return false;

            geoVolume.OnTerraform(worldPosition, region.BoundingVoxelCount);

            var voxels = new NativeList<int3>(region.BoundingVoxelCount, Allocator.TempJob);
            var smoothedVoxels = new NativeList<int3>(region.BoundingVoxelCount, Allocator.TempJob);
            var deltas = new NativeList<short>(region.BoundingVoxelCount, Allocator.TempJob);

            try {
                region.CollectVoxels(voxels);

                var solidMaterialCounts = new int[VoxelEncoding.NullMaterialIndex];
                CollectSmoothingDeltas(geoVolume, voxels, smoothedVoxels, deltas, solidMaterialCounts);

                TerraformCommandUtility.DispatchVoxelDeltas(
                    manager, geoVolume, worldPosition, smoothedVoxels, deltas, VoxelEditKind.DensityDelta,
                    MostCommonMaterial(solidMaterialCounts), nameof(TerraformSmoothCommand));
            }
            finally {
                voxels.Dispose();
                smoothedVoxels.Dispose();
                deltas.Dispose();
            }

            return true;
        }

        private static void CollectSmoothingDeltas(
            IGeoVolume geoVolume, NativeList<int3> voxels, NativeList<int3> smoothedVoxels, NativeList<short> deltas,
            int[] solidMaterialCounts) {
            ref var config = ref geoVolume.ConfigBlob.Value;
            var chunkSize = config.ChunkSize;
            var areaSize = config.ChunkDataAreaSize;
            var widthSize = config.ChunkDataWidthSize;

            for (var index = 0; index < voxels.Length; index++) {
                var voxel = voxels[index];
                var chunkCoord = TerraformCommandUtility.GetChunkCoord(voxel, chunkSize);
                var chunk = geoVolume.GetChunkByCoord(new Vector3Int(chunkCoord.x, chunkCoord.y, chunkCoord.z));

                if (chunk == null || !chunk.GeoChunkEngine.CanResolveEdits())
                    continue;

                var store = chunk.GeoChunkEngine.IGeoEditStore;
                var local = voxel - chunkCoord * chunkSize;
                var densitySum = 0;

                for (var offsetZ = -NeighbourhoodRadius; offsetZ <= NeighbourhoodRadius; offsetZ++)
                for (var offsetY = -NeighbourhoodRadius; offsetY <= NeighbourhoodRadius; offsetY++)
                for (var offsetX = -NeighbourhoodRadius; offsetX <= NeighbourhoodRadius; offsetX++) {
                    var neighbourIndex = GfStaticHelper.Coord3DToIndex(
                        local.x + offsetX, local.y + offsetY, local.z + offsetZ, areaSize, widthSize);

                    store.TryRead(neighbourIndex, out var neighbour);
                    densitySum += neighbour.Density;

                    var neighbourMaterial = neighbour.GetPlainMatIndex();

                    if (VoxelEncoding.IsSolid(neighbour.Density) && neighbourMaterial < solidMaterialCounts.Length)
                        solidMaterialCounts[neighbourMaterial]++;
                }

                var voxelIndex = GfStaticHelper.Coord3DToIndex(local.x, local.y, local.z, areaSize, widthSize);
                store.TryRead(voxelIndex, out var current);

                var smoothedDensity = Mathf.Clamp(
                    Mathf.RoundToInt(densitySum / NeighbourhoodVoxelCount), sbyte.MinValue, sbyte.MaxValue);
                var delta = smoothedDensity - current.Density;

                if (delta == 0)
                    continue;

                smoothedVoxels.Add(voxel);
                deltas.Add((short)delta);
            }
        }

        private static byte MostCommonMaterial(int[] solidMaterialCounts) {
            var mostCommon = VoxelEncoding.NullMaterialIndex;
            var highestCount = 0;

            for (var material = 0; material < solidMaterialCounts.Length; material++) {
                if (solidMaterialCounts[material] <= highestCount)
                    continue;

                highestCount = solidMaterialCounts[material];
                mostCommon = (byte)material;
            }

            return mostCommon;
        }
    }
}
