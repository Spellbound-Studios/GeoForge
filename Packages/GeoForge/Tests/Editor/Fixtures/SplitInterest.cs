// Copyright (c) Spellbound Studios. All rights reserved.

using System;

namespace Spellbound.GeoForge.Tests {
    internal sealed class SplitInterest : IChunkInterest {
        private readonly float _splitX;

        internal SplitInterest(float splitX, int levelBelowSplit, int levelAboveSplit) {
            _splitX = splitX;
            LevelBelowSplit = levelBelowSplit;
            LevelAboveSplit = levelAboveSplit;
        }

        internal int LevelBelowSplit { get; set; }

        internal int LevelAboveSplit { get; set; }

        public int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace) {
            return request.NodeBoundsVoxelSpace.center.x < _splitX ? LevelBelowSplit : LevelAboveSplit;
        }
    }
}
