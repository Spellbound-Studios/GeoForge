// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    [BurstCompile]
    internal struct TerraformVoxelScatterJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        [ReadOnly] public NativeArray<int3> Voxels;
        [ReadOnly] public NativeArray<short> Deltas;
        public NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>.ParallelWriter Writer;

        public void Execute(int index) {
            ref var config = ref ConfigBlob.Value;

            TerraformCommandUtility.ScatterVoxelDelta(
                Voxels[index], Deltas[index], config.ChunkSize, config.ChunkDataAreaSize, config.ChunkDataWidthSize,
                Writer);
        }
    }
}
