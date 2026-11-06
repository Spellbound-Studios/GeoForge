// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Blob Asset to hold Marching Cubes Tables.
    /// </summary>
    public struct McTablesBlobAsset {
        // Mapping of Voxel.matIndex to Colors for vertexColors
        public BlobArray<Color32> ColorsByIndex;

        // Regular marching cubes tables
        public BlobArray<int3> RegularCornerOffset;
        public BlobArray<byte> RegularCellClass;
        public BlobArray<long> VertexCount;
        public BlobArray<long> TriangleCount;
        public BlobArray<BlobArray<byte>> Indices;
        public BlobArray<BlobArray<ushort>> RegularVertexData;

        // Transition tables.
        public BlobArray<int3> TransitionCornerOffset;
        public BlobArray<byte> TransitionCellClass;
        public BlobArray<long> TransitionVertexCount;
        public BlobArray<long> TransitionTriangleCount;
        public BlobArray<BlobArray<byte>> TransitionIndices;
        public BlobArray<byte> TransitionCornerData;
        public BlobArray<BlobArray<ushort>> TransitionVertexData;
    }
}