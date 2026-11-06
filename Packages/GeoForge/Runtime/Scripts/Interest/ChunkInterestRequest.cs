// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    public readonly struct ChunkInterestRequest {
        public ChunkInterestRequest(
            Bounds nodeBoundsVoxelSpace,
            float resolution,
            NativeArray<Vector2>.ReadOnly levelOfDetailRanges,
            bool chunkHasEdits) {
            NodeBoundsVoxelSpace = nodeBoundsVoxelSpace;
            Resolution = resolution;
            LevelOfDetailRanges = levelOfDetailRanges;
            ChunkHasEdits = chunkHasEdits;
        }

        public Bounds NodeBoundsVoxelSpace { get; }

        public float Resolution { get; }

        public NativeArray<Vector2>.ReadOnly LevelOfDetailRanges { get; }

        public bool ChunkHasEdits { get; }
    }
}
