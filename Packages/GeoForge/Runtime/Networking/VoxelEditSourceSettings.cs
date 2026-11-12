// Copyright (c) Spellbound Studios. All rights reserved.

using System;

namespace Spellbound.GeoForge.Networking {
    public readonly struct VoxelEditSourceSettings {
        public const int DefaultLargestEditExtent = 32;
        public const float DefaultRegionCellSizeMetres = 64f;

        private readonly int _largestEditExtent;
        private readonly float _regionCellSizeMetres;

        public VoxelEditSourceSettings(ulong baseFingerprint, uint sourceId)
            : this(baseFingerprint, sourceId, null, 0, 0f) { }

        public VoxelEditSourceSettings(ulong baseFingerprint, uint sourceId, IChunkRegionMap chunkRegions)
            : this(baseFingerprint, sourceId, chunkRegions, 0, 0f) { }

        public VoxelEditSourceSettings(ulong baseFingerprint, uint sourceId, IChunkRegionMap chunkRegions,
            int largestEditExtent, float regionCellSizeMetres) {
            if (largestEditExtent < 0)
                throw new ArgumentOutOfRangeException(nameof(largestEditExtent), largestEditExtent,
                    "The largest edit extent is zero, for the default, or more.");

            if (float.IsNaN(regionCellSizeMetres) || float.IsInfinity(regionCellSizeMetres) ||
                regionCellSizeMetres < 0f)
                throw new ArgumentOutOfRangeException(nameof(regionCellSizeMetres), regionCellSizeMetres,
                    "The region cell size is zero, for the default, or a finite size above zero.");

            BaseFingerprint = baseFingerprint;
            SourceId = sourceId;
            ChunkRegions = chunkRegions;
            _largestEditExtent = largestEditExtent;
            _regionCellSizeMetres = regionCellSizeMetres;
        }

        public ulong BaseFingerprint { get; }

        public uint SourceId { get; }

        public IChunkRegionMap ChunkRegions { get; }

        public int LargestEditExtent => _largestEditExtent > 0 ? _largestEditExtent : DefaultLargestEditExtent;

        public float RegionCellSizeMetres =>
            _regionCellSizeMetres > 0f ? _regionCellSizeMetres : DefaultRegionCellSizeMetres;
    }
}