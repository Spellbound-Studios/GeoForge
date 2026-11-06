// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public readonly struct ChunkFillRequest {
        public ChunkFillRequest(
            Vector3Int chunkCoordinate,
            Vector3Int originVoxel,
            float voxelSize,
            int chunkSize,
            int dataWidth,
            uint seed) {
            ChunkCoordinate = chunkCoordinate;
            OriginVoxel = originVoxel;
            VoxelSize = voxelSize;
            ChunkSize = chunkSize;
            DataWidth = dataWidth;
            Seed = seed;
        }

        public Vector3Int ChunkCoordinate { get; }

        public Vector3Int OriginVoxel { get; }

        public float VoxelSize { get; }

        public int ChunkSize { get; }

        public int DataWidth { get; }

        public uint Seed { get; }

        public int DataVolume => DataWidth * DataWidth * DataWidth;

        internal static ChunkFillRequest ForChunk(
            Vector3Int chunkCoordinate, in VolumeConfigBlobAsset config, uint seed) {
            var origin = ChunkLayout.ChunkOrigin(new int3(chunkCoordinate.x, chunkCoordinate.y, chunkCoordinate.z),
                config);

            return new ChunkFillRequest(chunkCoordinate, new Vector3Int(origin.x, origin.y, origin.z),
                config.Resolution, config.ChunkSize, ChunkLayout.DataWidth(config.ChunkSize), seed);
        }
    }
}
