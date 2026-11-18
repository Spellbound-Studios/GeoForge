// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.GeoForge.Places {
    public static class VoxelLayerBaker {
        public static VoxelLayer Bake(GeoForgeDataGenerator generator, VoxelVolumeConfig config, VoxelRegion region,
            string layerName, uint seed) {
            if (generator == null) {
                throw new ArgumentNullException(nameof(generator));
            }

            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }

            var blob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);

            try {
                var chunkSize = blob.Value.ChunkSize;
                var dataWidth = blob.Value.ChunkDataWidthSize;
                var voxelSize = blob.Value.Resolution;

                if (chunkSize < 1) {
                    throw new ArgumentException("voxel config " + config.name + " has no chunk size", nameof(config));
                }

                var source = GeoForgeKit.CreateDensitySource(generator, blob);
                var chunks = new List<VoxelLayerChunk>(region.ChunkCount);

                foreach (var coordinate in region.Chunks()) {
                    var request = new ChunkFillRequest(coordinate, coordinate * chunkSize, voxelSize, chunkSize,
                        dataWidth, seed);
                    chunks.Add(new VoxelLayerChunk(region.OffsetOf(coordinate), Compress(source, in request)));
                }

                var layer = new VoxelLayer { LayerName = layerName };
                layer.SetChunks(chunkSize, region.ChunkExtent, chunks);

                return layer;
            } finally {
                blob.Dispose();
            }
        }

        public static float VoxelSize(VoxelVolumeConfig config) {
            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }

            var blob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);

            try {
                return blob.Value.Resolution;
            } finally {
                blob.Dispose();
            }
        }

        private static SparseVoxelData[] Compress(IDensitySource source, in ChunkFillRequest request) {
            var voxels = GeoForgeKit.Generate(source, in request, Allocator.TempJob);
            var runs = new NativeList<SparseVoxelData>(Allocator.TempJob);
            var densityRange = new NativeArray<DensityRange>(1, Allocator.TempJob);

            try {
                new DenseToSparseVoxelDataJob {
                    Voxels = voxels,
                    SparseVoxels = runs,
                    DensityRange = densityRange
                }.Run();

                return runs.AsArray().ToArray();
            } finally {
                densityRange.Dispose();
                runs.Dispose();
                voxels.Dispose();
            }
        }
    }
}
