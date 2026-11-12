// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.GeoForge.Networking.Tests {
    internal sealed class GroundDensitySource : IDensitySource {
        private const float Gradient = 16f;
        private const byte Material = 1;

        private readonly float _height;

        internal GroundDensitySource(float height) {
            _height = height;
        }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            dependency.Complete();
            var area = request.DataWidth * request.DataWidth;

            for (var index = 0; index < voxels.Length; index++) {
                var y = index / area;
                var signedDistance = (request.OriginVoxel.y + y) * request.VoxelSize - _height;
                var density = VoxelEncoding.DensityFromSignedDistance(signedDistance, Gradient);
                voxels[index] = VoxelData.CreateImmature(density, Material);
            }

            return default;
        }
    }
}