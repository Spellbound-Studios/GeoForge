// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [Serializable]
    public sealed class VoxelLayerChunk {
        [SerializeField] private Vector3Int offset;
        [SerializeField] private List<SparseVoxelData> runs = new();

        public VoxelLayerChunk() {
        }

        public VoxelLayerChunk(Vector3Int offset, IReadOnlyList<SparseVoxelData> runs) {
            if (runs == null) {
                throw new ArgumentNullException(nameof(runs));
            }

            this.offset = offset;
            this.runs = new List<SparseVoxelData>(runs);
        }

        public Vector3Int Offset => offset;

        public IReadOnlyList<SparseVoxelData> Runs => runs;
    }
}
