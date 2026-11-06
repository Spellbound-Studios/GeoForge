// Copyright (c) Spellbound Studios. All rights reserved.

using System;

namespace Spellbound.GeoForge {
    public interface IChunkInterest {
        int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace);
    }
}
