// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.ReferenceFrames;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    internal sealed class ColumnChunkRegionMap : IChunkRegionMap {
        private readonly int _lowestChunk;
        private readonly int _highestChunk;

        internal ColumnChunkRegionMap(int lowestChunk, int highestChunk) {
            _lowestChunk = lowestChunk;
            _highestChunk = highestChunk;
        }

        internal int RegionsAsked { get; private set; }

        public RegionKey RegionOf(Vector3Int chunkCoordinate) {
            RegionsAsked++;

            return new RegionKey(ReferenceFrameId.World, new RegionCell(chunkCoordinate.x, 0, chunkCoordinate.z));
        }

        public int ChunksOf(in RegionKey key, List<Vector3Int> chunks) {
            chunks.Clear();

            for (var y = _lowestChunk; y <= _highestChunk; y++)
                chunks.Add(new Vector3Int(key.Cell.X, y, key.Cell.Z));

            return chunks.Count;
        }
    }
}