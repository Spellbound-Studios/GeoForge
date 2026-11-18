// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [CreateAssetMenu(fileName = "VoxelRuns", menuName = "Spellbound/GeoForge/Voxel Runs")]
    public sealed class VoxelRunStore : ScriptableObject {
        [SerializeField] private List<VoxelLayerChunk> chunks = new();

        public IReadOnlyList<VoxelLayerChunk> Chunks => chunks;

        public void SetChunks(IReadOnlyList<VoxelLayerChunk> chunks) {
            if (chunks == null) {
                throw new ArgumentNullException(nameof(chunks));
            }

            this.chunks = new List<VoxelLayerChunk>(chunks);
        }
    }
}
