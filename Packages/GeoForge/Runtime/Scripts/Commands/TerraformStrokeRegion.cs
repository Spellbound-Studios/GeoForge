// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    internal readonly struct TerraformStrokeRegion {
        private readonly float _sphereHalfSizeVoxels;
        private readonly bool _isSphere;

        private TerraformStrokeRegion(
            Vector3Int centerVoxel, int boundingHalfExtent, float sphereHalfSizeVoxels, bool isSphere) {
            CenterVoxel = centerVoxel;
            BoundingHalfExtent = boundingHalfExtent;
            _sphereHalfSizeVoxels = sphereHalfSizeVoxels;
            _isSphere = isSphere;
        }

        internal Vector3Int CenterVoxel { get; }

        internal int BoundingHalfExtent { get; }

        internal int BoundingVoxelCount {
            get {
                var side = BoundingHalfExtent * 2 + 1;

                return side * side * side;
            }
        }

        internal static TerraformStrokeRegion Sphere(IGeoVolume geoVolume, Vector3 worldPosition, float size) {
            var halfSizeVoxels = size * 0.5f / geoVolume.ConfigBlob.Value.Resolution;

            return new TerraformStrokeRegion(
                geoVolume.WorldToVoxelSpace(worldPosition), Mathf.Max(0, Mathf.CeilToInt(halfSizeVoxels)),
                halfSizeVoxels, true);
        }

        internal static TerraformStrokeRegion Cube(IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent) {
            return new TerraformStrokeRegion(
                geoVolume.WorldToVoxelSpace(worldPosition), Mathf.Max(0, halfExtent), 0f, false);
        }

        internal bool TryValidate(
            IGeoVolume geoVolume, GeoForgeManager gfManager, string commandName, Vector3 worldPosition) {
            var paddedHalfExtent = BoundingHalfExtent + TerraformCommandUtility.PaddingMargin;

            return TerraformCommandUtility.TryValidateChunkRange(
                geoVolume, gfManager, CenterVoxel - Vector3Int.one * paddedHalfExtent,
                CenterVoxel + Vector3Int.one * paddedHalfExtent, commandName, worldPosition, out _, out _);
        }

        internal void CollectVoxels(NativeList<int3> voxels) {
            var center = new int3(CenterVoxel.x, CenterVoxel.y, CenterVoxel.z);
            var squaredHalfSize = _sphereHalfSizeVoxels * _sphereHalfSizeVoxels;

            for (var dz = -BoundingHalfExtent; dz <= BoundingHalfExtent; dz++)
            for (var dy = -BoundingHalfExtent; dy <= BoundingHalfExtent; dy++)
            for (var dx = -BoundingHalfExtent; dx <= BoundingHalfExtent; dx++) {
                if (_isSphere && dx * dx + dy * dy + dz * dz > squaredHalfSize)
                    continue;

                voxels.Add(center + new int3(dx, dy, dz));
            }
        }
    }
}
