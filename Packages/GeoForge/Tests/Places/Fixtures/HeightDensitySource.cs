// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.GeoForge.Places.Tests {
    public sealed class HeightDensitySource : IDensitySource {
        private const float Gradient = 16f;
        private const byte Material = 1;

        private readonly float _height;

        public HeightDensitySource(float height) {
            _height = height;
        }

        public JobHandle ScheduleFill(in ChunkFillRequest request, NativeArray<VoxelData> voxels,
            JobHandle dependency) {
            dependency.Complete();
            var width = request.DataWidth;
            var area = width * width;

            for (var index = 0; index < voxels.Length; index++) {
                GfStaticHelper.IndexToInt3(index, area, width, out _, out var y, out _);
                var signedDistance = (request.OriginVoxel.y + y) * request.VoxelSize - _height;
                var density = VoxelEncoding.DensityFromSignedDistance(signedDistance, Gradient);
                voxels[index] = VoxelData.CreateImmature(density, Material);
            }

            return default;
        }
    }
}
