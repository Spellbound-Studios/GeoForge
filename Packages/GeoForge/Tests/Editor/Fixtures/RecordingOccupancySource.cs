// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core.Ground;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingOccupancySource : IDensitySource, IOccupancySource {
        private const byte Material = 1;

        internal RecordingOccupancySource(Occupancy answer, sbyte fillDensity) {
            Answer = answer;
            FillVoxel = VoxelData.CreateImmature(fillDensity, Material);
        }

        internal Occupancy Answer { get; set; }

        internal VoxelData FillVoxel { get; }

        internal int Fills { get; private set; }

        internal int Classifications { get; private set; }

        internal Bounds ClassifiedBounds { get; private set; }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            dependency.Complete();
            Fills++;

            for (var index = 0; index < voxels.Length; index++)
                voxels[index] = FillVoxel;

            return default;
        }

        public Occupancy Classify(in Bounds bounds) {
            Classifications++;
            ClassifiedBounds = bounds;

            return Answer;
        }
    }
}
