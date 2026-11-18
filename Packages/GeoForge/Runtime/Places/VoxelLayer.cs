// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Authoring;
using Spellbound.Core.Packing;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [Serializable]
    public sealed class VoxelLayer : PlaceLayer, IPacker {
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

            foreach (var chunk in chunks) {
                if (chunk == null) {
                    throw new ArgumentException("A chunk list holds no null chunk.", nameof(chunks));
                }
            }

            this.chunkSize = chunkSize;
            this.chunkExtent = chunkExtent;

            if (store != null) {
                store.SetChunks(chunks);
                this.chunks = new List<VoxelLayerChunk>();
            } else {
                this.chunks = new List<VoxelLayerChunk>(chunks);
            }
        }

        public void UseStore(VoxelRunStore store) {
            if (store == null) {
                throw new ArgumentNullException(nameof(store));
            }

            this.store = store;
            chunks = new List<VoxelLayerChunk>();
        }

        public void Pack(ref Span<byte> buffer) {
            var heldChunks = Chunks;
            Packer.WriteInt(ref buffer, chunkSize);
            WriteVector3Int(ref buffer, chunkExtent);
            Packer.WriteInt(ref buffer, heldChunks.Count);

            foreach (var chunk in heldChunks) {
                WriteVector3Int(ref buffer, chunk.Offset);

                var runs = chunk.Runs;
                Packer.WriteInt(ref buffer, runs.Count);

                foreach (var run in runs) {
                    Packer.WriteInt(ref buffer, run.StartIndex);
                    run.Voxel.Pack(ref buffer);
                }
            }
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            var readChunkSize = Packer.ReadInt(ref buffer);
            var readChunkExtent = ReadVector3Int(ref buffer);
            var chunkCount = ReadCount(ref buffer, "chunks");
            var readChunks = new List<VoxelLayerChunk>(chunkCount);

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++) {
                var offset = ReadVector3Int(ref buffer);
                var runCount = ReadCount(ref buffer, "runs");
                var runs = new List<SparseVoxelData>(runCount);

                for (var runIndex = 0; runIndex < runCount; runIndex++) {
                    var startIndex = Packer.ReadInt(ref buffer);
                    var voxel = new VoxelData();
                    voxel.Unpack(ref buffer);
                    runs.Add(new SparseVoxelData(voxel, startIndex));
                }

                readChunks.Add(new VoxelLayerChunk(offset, runs));
            }

            store = null;
            SetChunks(readChunkSize, readChunkExtent, readChunks);
        }

        public override void Validate(ValidationReport report, string location) {
            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            var heldChunks = Chunks;

            if (heldChunks.Count == 0) {
                report.Error(GeoForgePlacesFindingCodes.VoxelLayerEmpty,
                    "voxel layer '" + LayerName + "' holds no chunks", location);
                return;
            }

            var expectedChunks = chunkExtent.x * chunkExtent.y * chunkExtent.z;

            if (heldChunks.Count != expectedChunks) {
                report.Error(GeoForgePlacesFindingCodes.VoxelLayerChunksMismatch,
                    "voxel layer '" + LayerName + "' holds " + heldChunks.Count + " chunks where its extent "
                    + chunkExtent + " needs " + expectedChunks, location);
            }

            var dataVolume = ChunkDataVolume;

            for (var chunkIndex = 0; chunkIndex < heldChunks.Count; chunkIndex++) {
                var chunk = heldChunks[chunkIndex];

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

        private static void WriteVector3Int(ref Span<byte> buffer, Vector3Int value) {
            Packer.WriteInt(ref buffer, value.x);
            Packer.WriteInt(ref buffer, value.y);
            Packer.WriteInt(ref buffer, value.z);
        }

        private static Vector3Int ReadVector3Int(ref ReadOnlySpan<byte> buffer) {
            var x = Packer.ReadInt(ref buffer);
            var y = Packer.ReadInt(ref buffer);
            var z = Packer.ReadInt(ref buffer);

            return new Vector3Int(x, y, z);
        }

        private static int ReadCount(ref ReadOnlySpan<byte> buffer, string counted) {
            var count = Packer.ReadInt(ref buffer);

            if (count < 0 || count > buffer.Length) {
                throw new InvalidOperationException("A voxel layer names " + count + " " + counted + " with "
                                                    + buffer.Length + " bytes left.");
            }

            return count;
        }
    }
}
