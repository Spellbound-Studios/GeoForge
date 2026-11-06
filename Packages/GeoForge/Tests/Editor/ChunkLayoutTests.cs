// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class ChunkLayoutTests {
        private const float WeightTolerance = 1e-6f;
        private static readonly int[] ChunkSizes = { 8, 16, 32, 64, 128 };

        [Test]
        public void TheDataWidthCarriesThreeApronVoxels() {
            foreach (var chunkSize in ChunkSizes)
                Assert.AreEqual(chunkSize + 3, ChunkLayout.DataWidth(chunkSize));
        }

        [Test]
        public void TheIndexIsTheHelpersIndex() {
            foreach (var chunkSize in new[] { 8, 16 }) {
                var width = ChunkLayout.DataWidth(chunkSize);

                for (var y = 0; y < width; y++)
                for (var z = 0; z < width; z++)
                for (var x = 0; x < width; x++)
                    Assert.AreEqual(GfStaticHelper.Coord3DToIndex(x, y, z, width * width, width),
                        ChunkLayout.Index(x, y, z, width));
            }
        }

        [Test]
        public void TheChunkOriginIsTheHelpersOrigin() {
            var config = new VolumeConfigBlobAsset { ChunkSize = 16, Offset = new Vector3Int(-9, -1, -25) };

            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
            for (var z = -3; z <= 3; z++) {
                var chunkCoordinate = new int3(x, y, z);

                Assert.AreEqual(GfStaticHelper.GetChunkOrigin(chunkCoordinate, config),
                    ChunkLayout.ChunkOrigin(chunkCoordinate, config));
            }
        }

        [Test]
        public void AVoxelBelongsToTheChunkThatOwnsVoxelsOneToChunkSize() {
            foreach (var chunkSize in ChunkSizes) {
                for (var boundary = -3; boundary <= 3; boundary++) {
                    for (var delta = -2; delta <= 2; delta++) {
                        var value = boundary * chunkSize + delta;
                        var position = new Vector3Int(value, value + 1, value - 1);

                        Assert.AreEqual(VolumeRuleCoordinate(position, chunkSize),
                            ChunkLayout.CoordinateOfVoxel(position, chunkSize), $"position {position}");
                    }
                }

                Assert.AreEqual(Vector3Int.zero, ChunkLayout.CoordinateOfVoxel(Vector3Int.one, chunkSize));
                Assert.AreEqual(Vector3Int.zero, ChunkLayout.CoordinateOfVoxel(Vector3Int.one * chunkSize, chunkSize));
                Assert.AreEqual(Vector3Int.one, ChunkLayout.CoordinateOfVoxel(Vector3Int.one * (chunkSize + 1),
                    chunkSize));
                Assert.AreEqual(-Vector3Int.one, ChunkLayout.CoordinateOfVoxel(Vector3Int.zero, chunkSize));
            }
        }

        [Test]
        public void TheCornerWeightsAreTheColumnCornerWeightsOfSixteenVoxelChunks() {
            var expected = new (int X, int Z, float4 Weights)[] {
                (0, 0, new float4(1f, 0f, 0f, 0f)),
                (1, 1, new float4(1f, 0f, 0f, 0f)),
                (1, 17, new float4(0f, 1f, 0f, 0f)),
                (17, 1, new float4(0f, 0f, 1f, 0f)),
                (17, 17, new float4(0f, 0f, 0f, 1f)),
                (1, 9, new float4(0.5f, 0.5f, 0f, 0f)),
                (17, 5, new float4(0f, 0f, 0.84375f, 0.15625f)),
                (4, 1, new float4(0.907714844f, 0f, 0.0922851562f, 0f)),
                (12, 17, new float4(0f, 0.231933594f, 0f, 0.768066406f)),
                (9, 9, new float4(0.25f, 0.25f, 0.25f, 0.25f)),
                (5, 13, new float4(0.131835938f, 0.711914062f, 0.0244140625f, 0.131835938f)),
                (18, 18, new float4(0f, 0f, 0f, 1f)),
                (0, 9, new float4(0.5f, 0.5f, 0f, 0f))
            };

            using var weights = ChunkLayout.CornerWeights(16, Allocator.Temp);
            var width = ChunkLayout.DataWidth(16);

            Assert.AreEqual(width * width, weights.Length);

            foreach (var (x, z, cornerWeights) in expected) {
                var actual = weights[x + z * width];

                for (var corner = 0; corner < 4; corner++)
                    Assert.AreEqual(cornerWeights[corner], actual[corner], WeightTolerance, $"column {x}, {z}");
            }
        }

        [Test]
        public void TheCornerWeightsAreTheInterpolationBlobsWeights() {
            foreach (var chunkSize in ChunkSizes) {
                var blob = McChunkInterpolationBlobCreator.CreateMcChunkInterpolationBlobAsset(chunkSize);

                try {
                    using var weights = ChunkLayout.CornerWeights(chunkSize, Allocator.Temp);
                    ref var blobWeights = ref blob.Value.ChunkCornerWeights;

                    Assert.AreEqual(blobWeights.Length, weights.Length);

                    for (var index = 0; index < weights.Length; index++)
                        Assert.AreEqual(blobWeights[index], weights[index], $"chunk size {chunkSize}, column {index}");
                }
                finally {
                    blob.Dispose();
                }
            }
        }

        private static Vector3Int VolumeRuleCoordinate(Vector3Int voxelPosition, int chunkSize) {
            return new Vector3Int(
                Mathf.FloorToInt((voxelPosition.x - 1f) / chunkSize),
                Mathf.FloorToInt((voxelPosition.y - 1f) / chunkSize),
                Mathf.FloorToInt((voxelPosition.z - 1f) / chunkSize));
        }
    }
}
