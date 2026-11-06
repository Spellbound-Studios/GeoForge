// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class SingleNodeInterest : IChunkInterest {
        private const float SameCentre = 0.5f;

        private readonly Vector3 _wantedCentre;

        internal SingleNodeInterest(Vector3 wantedCentre) {
            _wantedCentre = wantedCentre;
        }

        internal int Questions { get; private set; }

        internal int ViewersSeen { get; private set; }

        public int DetailLevel(in ChunkInterestRequest request, ReadOnlySpan<ChunkViewer> viewersVoxelSpace) {
            Questions++;
            ViewersSeen = viewersVoxelSpace.Length;

            return Vector3.Distance(request.NodeBoundsVoxelSpace.center, _wantedCentre) < SameCentre
                ? 0
                : ChunkInterest.NotWanted;
        }
    }
}
