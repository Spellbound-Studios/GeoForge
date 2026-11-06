// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Precomputation for interpolation of corner weights across a chunk.
    ///     Just API to help generating smooth biomes.
    /// </summary>
    public struct McChunkInterpolationBlobAsset {
        public BlobArray<float4> ChunkCornerWeights;
    }

    public static class McChunkInterpolationBlobCreator {
        public static BlobAssetReference<McChunkInterpolationBlobAsset>
            CreateMcChunkInterpolationBlobAsset(int chunkSize) {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var chunkInterpolations = ref builder.ConstructRoot<McChunkInterpolationBlobAsset>();

            var width = ChunkLayout.DataWidth(chunkSize);
            var weightArray = builder.Allocate(ref chunkInterpolations.ChunkCornerWeights, width * width);
            using var weights = ChunkLayout.CornerWeights(chunkSize, Allocator.Temp);

            for (var i = 0; i < weightArray.Length; i++)
                weightArray[i] = weights[i];

            var result = builder.CreateBlobAssetReference<McChunkInterpolationBlobAsset>(Allocator.Persistent);
            builder.Dispose();

            return result;
        }
    }
}