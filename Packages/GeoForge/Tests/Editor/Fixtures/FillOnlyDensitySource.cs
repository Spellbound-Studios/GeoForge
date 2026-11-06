// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.GeoForge.Tests {
    internal sealed class FillOnlyDensitySource : IDensitySource {
        private readonly IDensitySource _filler;

        internal FillOnlyDensitySource(IDensitySource filler) {
            _filler = filler;
        }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            return _filler.ScheduleFill(request, voxels, dependency);
        }
    }
}
