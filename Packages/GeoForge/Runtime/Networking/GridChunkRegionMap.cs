// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.ReferenceFrames;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    public sealed class GridChunkRegionMap : IChunkRegionMap {
        private readonly IGeoVolume _volume;
        private readonly GridRegionGeometry _geometry;

        public GridChunkRegionMap(IGeoVolume volume, float cellSizeMetres) {
            _volume = volume ?? throw new ArgumentNullException(nameof(volume));
            _geometry = new GridRegionGeometry(cellSizeMetres);
        }

        public float CellSizeMetres => _geometry.CellSize;

        public RegionKey RegionOf(Vector3Int chunkCoordinate) {
            return _geometry.KeyOf(ReferenceFrameId.World, EngineOf().ChunkWorldOrigin(chunkCoordinate));
        }

        public int ChunksOf(in RegionKey key, List<Vector3Int> chunks) {
            if (chunks == null)
                throw new ArgumentNullException(nameof(chunks));

            chunks.Clear();

            if (key.Frame != ReferenceFrameId.World)
                return 0;

            var engine = EngineOf();
            var size = _geometry.CellSize;
            var corner = new Vector3(key.Cell.X * size, key.Cell.Y * size, key.Cell.Z * size);
            var lowest = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var highest = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            for (var z = 0; z <= 1; z++) {
                var chunk = engine.GetCoordByVoxelPosition(
                    engine.WorldToVoxelSpace(corner + new Vector3(x * size, y * size, z * size)));
                lowest = Vector3Int.Min(lowest, chunk);
                highest = Vector3Int.Max(highest, chunk);
            }

            lowest -= Vector3Int.one;
            highest += Vector3Int.one;

            for (var x = lowest.x; x <= highest.x; x++)
            for (var y = lowest.y; y <= highest.y; y++)
            for (var z = lowest.z; z <= highest.z; z++) {
                var chunk = new Vector3Int(x, y, z);

                if (RegionOf(chunk) == key)
                    chunks.Add(chunk);
            }

            return chunks.Count;
        }

        private GeoVolumeEngine EngineOf() {
            return _volume.GeoVolumeEngine ?? throw new InvalidOperationException(
                "The volume has no engine yet, so its chunks have no place in the world to map to a region.");
        }
    }
}