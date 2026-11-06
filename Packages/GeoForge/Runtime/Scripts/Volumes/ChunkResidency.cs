// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public readonly struct ChunkResidency {
        public const int NoBuiltLevel = -1;

        public ChunkResidency(Vector3Int chunkCoordinate, ChunkResidencyState state, int finestBuiltLevel) {
            ChunkCoordinate = chunkCoordinate;
            State = state;
            FinestBuiltLevel = finestBuiltLevel;
        }

        public Vector3Int ChunkCoordinate { get; }

        public ChunkResidencyState State { get; }

        public int FinestBuiltLevel { get; }
    }
}
