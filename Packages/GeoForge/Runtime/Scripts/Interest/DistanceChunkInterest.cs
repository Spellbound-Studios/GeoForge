// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge {
    public sealed class DistanceChunkInterest : IChunkInterest {
        public int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace) {
            var nodeCentreVoxelSpace = request.NodeBoundsVoxelSpace.center;
            var nearest = float.MaxValue;
            var insideDetailRadius = false;

            foreach (var viewer in viewersVoxelSpace) {
                var viewerDistance = Vector3.Distance(nodeCentreVoxelSpace, viewer.Position);
                nearest = Mathf.Min(nearest, viewerDistance);
                insideDetailRadius |= viewer.DetailRadius <= 0f || viewerDistance <= viewer.DetailRadius;
            }

            if (!insideDetailRadius)
                return ChunkInterest.NotWanted;

            var distance = nearest * request.Resolution;
            var ranges = request.LevelOfDetailRanges;

            for (var level = 0; level < ranges.Length; level++)
                if (distance <= ranges[level].y)
                    return level;

            return ranges.Length - 1;
        }
    }
}
