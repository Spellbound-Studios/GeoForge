// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Spellbound.GeoForge {
    internal sealed class DataGeneratorDensitySource : IDensitySource {
        private readonly BlobAssetReference<VolumeConfigBlobAsset> _configBlob;
        private readonly GeoForgeDataGenerator _generator;

        internal DataGeneratorDensitySource(
            GeoForgeDataGenerator generator, BlobAssetReference<VolumeConfigBlobAsset> configBlob) {
            _generator = generator;
            _configBlob = configBlob;
        }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            dependency.Complete();

            if (_generator is DataFactory factory) {
                factory.FillDataArray(request.ChunkCoordinate, _configBlob, voxels);

                return dependency;
            }

            var generated = _generator.GenerateProceduralVoxels(request.ChunkCoordinate, _configBlob, request.Seed);

            try {
                voxels.CopyFrom(generated);
            }
            finally {
                generated.Dispose();
            }

            return dependency;
        }
    }
}
