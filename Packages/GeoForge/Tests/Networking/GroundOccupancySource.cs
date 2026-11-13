// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core.Ground;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    internal sealed class GroundOccupancySource : IDensitySource, IOccupancySource {
        private const float Gradient = 16f;

        private readonly GroundDensitySource _ground;
        private readonly float _height;

        internal GroundOccupancySource(float height) {
            _height = height;
            _ground = new GroundDensitySource(height);
        }

        internal int Fills { get; private set; }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            Fills++;

            return _ground.ScheduleFill(request, voxels, dependency);
        }

        public Occupancy Classify(in Bounds bounds) {
            if (bounds.max.y < _height)
                return Occupancy.Solid;

            return bounds.min.y > _height + 1f / Gradient ? Occupancy.Empty : Occupancy.Mixed;
        }
    }
}
