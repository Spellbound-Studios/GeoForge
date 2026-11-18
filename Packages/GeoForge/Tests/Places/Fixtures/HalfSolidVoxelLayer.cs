// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public static class HalfSolidVoxelLayer {
        public const int ChunkSize = 8;
        public const int FirstOpenRow = 5;

        public static VoxelLayer Create() {
            var width = ChunkLayout.DataWidth(ChunkSize);
            var runs = new[] {
                new SparseVoxelData(VoxelData.CreateMature(127, 1), 0),
                new SparseVoxelData(VoxelData.CreateImmature(-128, 0), FirstOpenRow * width * width)
            };
            var layer = new VoxelLayer { LayerName = "voxels" };
            layer.SetChunks(ChunkSize, Vector3Int.one, new[] { new VoxelLayerChunk(Vector3Int.zero, runs) });
            return layer;
        }
    }
}
