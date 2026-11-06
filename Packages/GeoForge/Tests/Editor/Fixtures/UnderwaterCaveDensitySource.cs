// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class UnderwaterCaveDensitySource : IDensitySource {
        private readonly Vector3 _cavityCenter;
        private readonly float _cavityRadius;
        private readonly float _gradient;
        private readonly byte _material;
        private readonly float _seaFloorHeight;

        internal UnderwaterCaveDensitySource(
            float seaFloorHeight, Vector3 cavityCenter, float cavityRadius, float gradient, byte material) {
            _seaFloorHeight = seaFloorHeight;
            _cavityCenter = cavityCenter;
            _cavityRadius = cavityRadius;
            _gradient = gradient;
            _material = material;
        }

        internal float SignedDistance(Vector3 position) {
            var aboveSeaFloor = position.y - _seaFloorHeight;
            var insideCavity = _cavityRadius - Vector3.Distance(position, _cavityCenter);

            return Mathf.Max(aboveSeaFloor, insideCavity);
        }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            dependency.Complete();
            var dataWidth = request.DataWidth;

            for (var index = 0; index < voxels.Length; index++) {
                GfStaticHelper.IndexToInt3(index, dataWidth * dataWidth, dataWidth, out var x, out var y, out var z);
                var position = request.OriginVoxel + new Vector3Int(x, y, z);
                var density = VoxelEncoding.DensityFromSignedDistance(SignedDistance(position), _gradient);
                voxels[index] = VoxelData.CreateImmature(density, _material);
            }

            return dependency;
        }
    }
}
