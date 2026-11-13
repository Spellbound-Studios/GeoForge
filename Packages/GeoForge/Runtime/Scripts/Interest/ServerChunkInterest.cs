// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Ground;
using UnityEngine;

namespace Spellbound.GeoForge {
    public sealed class ServerChunkInterest : IChunkInterest {
        private readonly IGeoVolume _volume;

        public ServerChunkInterest(IGeoVolume volume) {
            _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        }

        public bool SkipByOccupancy { get; set; }

        public int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace) {
            var nodeBoundsVoxelSpace = request.NodeBoundsVoxelSpace;

            if (SkipByOccupancy && !request.ChunkHasEdits && IsUniform(nodeBoundsVoxelSpace))
                return ChunkInterest.NotWanted;

            var nodeCentreVoxelSpace = nodeBoundsVoxelSpace.center;

            foreach (var viewer in viewersVoxelSpace) {
                var offset = nodeCentreVoxelSpace - viewer.Position;
                var radius = viewer.CollisionRadius;

                if (Mathf.Abs(offset.x) <= radius && Mathf.Abs(offset.y) <= radius && Mathf.Abs(offset.z) <= radius)
                    return 0;
            }

            return ChunkInterest.NotWanted;
        }

        private bool IsUniform(in Bounds nodeBoundsVoxelSpace) {
            if (_volume.DensitySource is not IOccupancySource occupancy)
                return false;

            return occupancy.Classify(ChunkLayout.FillSpaceBounds(nodeBoundsVoxelSpace, _volume.ConfigBlob.Value)) !=
                   Occupancy.Mixed;
        }
    }
}
