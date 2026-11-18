// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [Serializable]
    public struct VoxelRegion : IEquatable<VoxelRegion> {
        [SerializeField] private Vector3Int minimumChunk;
        [SerializeField] private Vector3Int chunkExtent;

        public VoxelRegion(Vector3Int minimumChunk, Vector3Int chunkExtent) {
            if (chunkExtent.x < 1 || chunkExtent.y < 1 || chunkExtent.z < 1) {
                throw new ArgumentOutOfRangeException(nameof(chunkExtent), chunkExtent,
                    "A region is at least one chunk along each axis.");
            }

            this.minimumChunk = minimumChunk;
            this.chunkExtent = chunkExtent;
        }

        public static VoxelRegion Origin => new(Vector3Int.zero, Vector3Int.one);

        public Vector3Int MinimumChunk => minimumChunk;

        public Vector3Int ChunkExtent => chunkExtent;

        public int ChunkCount => chunkExtent.x * chunkExtent.y * chunkExtent.z;

        public bool Contains(Vector3Int chunkCoordinate) {
            var offset = OffsetOf(chunkCoordinate);
            return offset.x >= 0 && offset.y >= 0 && offset.z >= 0
                   && offset.x < chunkExtent.x && offset.y < chunkExtent.y && offset.z < chunkExtent.z;
        }

        public IEnumerable<Vector3Int> Chunks() {
            for (var z = 0; z < chunkExtent.z; z++) {
                for (var y = 0; y < chunkExtent.y; y++) {
                    for (var x = 0; x < chunkExtent.x; x++) {
                        yield return minimumChunk + new Vector3Int(x, y, z);
                    }
                }
            }
        }

        public Vector3Int OffsetOf(Vector3Int chunkCoordinate) {
            return chunkCoordinate - minimumChunk;
        }

        public bool Equals(VoxelRegion other) {
            return minimumChunk == other.minimumChunk && chunkExtent == other.chunkExtent;
        }

        public override bool Equals(object other) {
            return other is VoxelRegion region && Equals(region);
        }

        public override int GetHashCode() {
            return HashCode.Combine(minimumChunk, chunkExtent);
        }

        public static bool operator ==(VoxelRegion left, VoxelRegion right) {
            return left.Equals(right);
        }

        public static bool operator !=(VoxelRegion left, VoxelRegion right) {
            return !left.Equals(right);
        }

        public override string ToString() {
            return minimumChunk.x + "," + minimumChunk.y + "," + minimumChunk.z + " x " + chunkExtent.x + ","
                   + chunkExtent.y + "," + chunkExtent.z;
        }
    }
}
