// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge {
    internal struct ColliderBakeJob : IJobParallelFor {
        [ReadOnly] public NativeArray<EntityId> Meshes;

        public void Execute(int index) {
            Physics.BakeMesh(Meshes[index], false);
        }
    }
}
