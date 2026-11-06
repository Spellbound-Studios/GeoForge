// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.GeoForge {
    public interface IDensitySource {
        JobHandle ScheduleFill(in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency);
    }
}
