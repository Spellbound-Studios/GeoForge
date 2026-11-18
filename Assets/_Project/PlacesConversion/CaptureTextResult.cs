// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.GeoForge;
using UnityEngine;

namespace PlacesConversion {
    public sealed class CaptureTextResult {
        public CaptureTextResult(Vector3Int dimensions, List<SparseVoxelData> runs) {
            Dimensions = dimensions;
            Runs = runs;
        }

        public Vector3Int Dimensions { get; }

        public List<SparseVoxelData> Runs { get; }
    }
}