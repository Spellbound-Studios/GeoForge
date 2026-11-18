// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Places.Owners;
using Unity.Collections;

namespace Spellbound.GeoForge.Places {
    [PlaceLayerOwner]
    public sealed class VoxelLayerOwner : IPlaceLayerOwner<VoxelLayer> {
        private const string VolumeMissingMessage = "no GeoForge volume with a running engine is the owner's target";

        public VoxelLayerOwner() {
            Region = VoxelRegion.Origin;
        }

        public VoxelLayerOwner(IGeoVolume volume, VoxelRegion region) {
            Volume = volume ?? throw new ArgumentNullException(nameof(volume));
            Region = region;
        }

        public IGeoVolume Volume { get; }

        public VoxelRegion Region { get; }

        public void Stamp(VoxelLayer layer, PlaceStampContext context) {
            if (layer == null) {
                throw new ArgumentNullException(nameof(layer));
            }

            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            var engine = ResolveEngine();

            if (engine == null) {
                context.Report.Error(GeoForgePlacesFindingCodes.VolumeMissing,
                    "voxel layer '" + layer.LayerName + "' is not stamped: " + VolumeMissingMessage, layer.LayerName);
                return;
            }

            var volumeChunkSize = VolumeChunkSize(engine);

            if (layer.ChunkSize != volumeChunkSize) {
                context.Report.Error(GeoForgePlacesFindingCodes.VoxelLayerChunksMismatch,
                    "voxel layer '" + layer.LayerName + "' holds chunks of " + layer.ChunkSize
                    + " voxels where the volume's chunks are " + volumeChunkSize, layer.LayerName);
                return;
            }

            var dataVolume = engine.ConfigBlob.Value.ChunkDataVolumeSize;
            var chunks = layer.Chunks;

            for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++) {
                var layerChunk = chunks[chunkIndex];
                var coordinate = Region.MinimumChunk + layerChunk.Offset;
                var volumeChunk = engine.GetChunkByCoord(coordinate);

                if (volumeChunk == null) {
                    context.Report.Error(GeoForgePlacesFindingCodes.ChunkNotLoaded,
                        "chunk " + coordinate + " is not loaded in the volume, so its runs are not stamped",
                        layer.LayerName + ".chunks[" + chunkIndex + "]");
                    continue;
                }

                if (context.Preview) {
                    continue;
                }

                WriteChunk(volumeChunk, layerChunk.Runs, dataVolume);
            }
        }

        public VoxelLayer Capture(PlaceCaptureContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            var engine = ResolveEngine();

            if (engine == null) {
                context.Report.Error(GeoForgePlacesFindingCodes.VolumeMissing,
                    "voxel layer '" + context.LayerName + "' is not captured: " + VolumeMissingMessage,
                    context.LayerName);
                return null;
            }

            var chunks = new List<VoxelLayerChunk>(Region.ChunkCount);
            var runs = new NativeList<SparseVoxelData>(Allocator.Temp);

            try {
                foreach (var coordinate in Region.Chunks()) {
                    if (!engine.TryCopyRunList(coordinate, runs, out _)) {
                        context.Report.Error(GeoForgePlacesFindingCodes.ChunkNotLoaded,
                            "chunk " + coordinate + " is not loaded in the volume, so it is not captured",
                            context.LayerName);
                        continue;
                    }

                    var managedRuns = new List<SparseVoxelData>(runs.Length);

                    for (var index = 0; index < runs.Length; index++) {
                        managedRuns.Add(runs[index]);
                    }

                    chunks.Add(new VoxelLayerChunk(Region.OffsetOf(coordinate), managedRuns));
                }
            } finally {
                runs.Dispose();
            }

            var layer = new VoxelLayer { LayerName = context.LayerName };
            layer.SetChunks(VolumeChunkSize(engine), Region.ChunkExtent, chunks);

            return layer;
        }

        private static void WriteChunk(IGeoChunk volumeChunk, IReadOnlyList<SparseVoxelData> runs, int dataVolume) {
            var voxels = new NativeArray<VoxelData>(dataVolume, Allocator.TempJob);
            var handedToChunk = false;

            try {
                VoxelRunDecoder.Decode(runs, voxels);
                handedToChunk = true;
                volumeChunk.GeoChunkEngine.SetVoxels(voxels);
            } finally {
                if (!handedToChunk) {
                    voxels.Dispose();
                }
            }
        }

        private static int VolumeChunkSize(GeoVolumeEngine engine) {
            return engine.ConfigBlob.Value.ChunkDataWidthSize - 3;
        }

        private GeoVolumeEngine ResolveEngine() {
            return Volume?.GeoVolumeEngine;
        }
    }
}
