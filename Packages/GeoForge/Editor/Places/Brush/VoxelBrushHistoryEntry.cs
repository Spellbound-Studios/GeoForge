// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [Serializable]
    public sealed class VoxelBrushHistoryEntry {
        [SerializeField] private VoxelStroke stroke;
        [SerializeField] private List<VoxelLayerChunk> before = new();
        [SerializeField] private List<VoxelLayerChunk> after = new();

        public VoxelBrushHistoryEntry() {
        }

        public VoxelBrushHistoryEntry(VoxelStroke stroke, IEnumerable<VoxelLayerChunk> before,
            IEnumerable<VoxelLayerChunk> after) {
            this.stroke = stroke;
            this.before = new List<VoxelLayerChunk>(before);
            this.after = new List<VoxelLayerChunk>(after);
        }

        public VoxelStroke Stroke => stroke;

        public IReadOnlyList<VoxelLayerChunk> Before => before;

        public IReadOnlyList<VoxelLayerChunk> After => after;
    }
}
