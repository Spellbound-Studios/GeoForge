// Copyright (c) Spellbound Studios. All rights reserved.

using System;

namespace Spellbound.GeoForge.Tests {
    internal sealed class ViewerRecordingInterest : IChunkInterest {
        internal ChunkViewer[] ViewersSeen { get; private set; } = Array.Empty<ChunkViewer>();

        public int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace) {
            ViewersSeen = viewersVoxelSpace.ToArray();

            return ChunkInterest.NotWanted;
        }
    }
}
