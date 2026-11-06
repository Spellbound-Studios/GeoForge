// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public readonly struct VolumeChange {
        public VolumeChange(Vector3Int chunkCoordinate, BoundsInt voxelBounds, VolumeVersion version) {
            ChunkCoordinate = chunkCoordinate;
            VoxelBounds = voxelBounds;
            Version = version;
        }

        public Vector3Int ChunkCoordinate { get; }

        public BoundsInt VoxelBounds { get; }

        public VolumeVersion Version { get; }
    }
}
