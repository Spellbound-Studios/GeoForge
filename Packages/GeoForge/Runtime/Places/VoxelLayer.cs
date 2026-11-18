// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [Serializable]
    public sealed class VoxelLayer : PlaceLayer {
        [SerializeField] private int chunkSize = 64;
        [SerializeField] private Vector3Int chunkExtent = Vector3Int.one;
        [SerializeField] private List<VoxelLayerChunk> chunks = new();
        [SerializeField] private VoxelRunStore store;

        public int ChunkSize => chunkSize;

        public Vector3Int ChunkExtent => chunkExtent;

        public IReadOnlyList<VoxelLayerChunk> Chunks => store != null ? store.Chunks : chunks;

        public VoxelRunStore Store => store;

        public int DataWidth => ChunkLayout.DataWidth(chunkSize);

        public int ChunkDataVolume => DataWidth * DataWidth * DataWidth;

        public void SetChunks(int chunkSize, Vector3Int chunkExtent, IReadOnlyList<VoxelLayerChunk> chunks) {
            if (chunks == null) {
                throw new ArgumentNullException(nameof(chunks));
            }

            if (chunkSize < 1) {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "A chunk is at least one voxel.");
            }

            if (chunkExtent.x < 1 || chunkExtent.y < 1 || chunkExtent.z < 1) {
                throw new ArgumentOutOfRangeException(nameof(chunkExtent), chunkExtent,
                    "A layer is at least one chunk along each axis.");
            }

            this.chunkSize = chunkSize;
            this.chunkExtent = chunkExtent;
            this.chunks = new List<VoxelLayerChunk>(chunks);
            store = null;
        }

        public void UseStore(VoxelRunStore store) {
            if (store == null) {
                throw new ArgumentNullException(nameof(store));
            }

            this.store = store;
            chunks = new List<VoxelLayerChunk>();
        }

        public override void Validate(ValidationReport report, string location) {
            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            var chunks = Chunks;

            if (chunks.Count == 0) {
                report.Error(GeoForgePlacesFindingCodes.VoxelLayerEmpty,
                    "voxel layer '" + LayerName + "' holds no chunks", location);
                return;
            }

            var expectedChunks = chunkExtent.x * chunkExtent.y * chunkExtent.z;

            if (chunks.Count != expectedChunks) {
                report.Error(GeoForgePlacesFindingCodes.VoxelLayerChunksMismatch,
                    "voxel layer '" + LayerName + "' holds " + chunks.Count + " chunks where its extent "
                    + chunkExtent + " needs " + expectedChunks, location);
            }

            var dataVolume = ChunkDataVolume;

            for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++) {
                var chunk = chunks[chunkIndex];

                if (chunk == null) {
                    continue;
                }

                foreach (var run in chunk.Runs) {
                    if (run.StartIndex < 0 || run.StartIndex >= dataVolume) {
                        report.Error(GeoForgePlacesFindingCodes.RunOutOfRange,
                            "a run starts at voxel " + run.StartIndex + ", outside the chunk's " + dataVolume
                            + " voxels", location + ".chunks[" + chunkIndex + "]");
                    }
                }
            }
        }
    }
}
