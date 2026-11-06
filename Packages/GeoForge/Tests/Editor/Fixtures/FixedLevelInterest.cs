// Copyright (c) Spellbound Studios. All rights reserved.

using System;

namespace Spellbound.GeoForge.Tests {
    internal sealed class FixedLevelInterest : IChunkInterest {
        private readonly int _level;

        internal FixedLevelInterest(int level) {
            _level = level;
        }

        public int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace) {
            return _level;
        }
    }
}
