// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelLayerPackingTests {
        private const string FixturePlacePath = "Assets/_Project/PlacesConversion/MainShaftBottomV2.asset";
        private const int FixtureRunCount = 124718;

        [Test]
        public void ATwoChunkLayerSurvivesARoundTrip() {
            var runs = VoxelLayerOwnerRoundTripTests.CanonicalRuns();
            var secondRuns = new List<SparseVoxelData> {
                new(VoxelData.CreateMature(-128, 3), 0),
                new(VoxelData.CreateImmature(90, 4), 1024)
            };
            var layer = VoxelLayerOwnerRoundTripTests.CreateLayer(new Vector3Int(2, 1, 1),
                new VoxelLayerChunk(Vector3Int.zero, runs), new VoxelLayerChunk(Vector3Int.right, secondRuns));

            var restored = RoundTrip(layer);

            Assert.IsNull(restored.Store);
            Assert.AreEqual(layer.ChunkSize, restored.ChunkSize);
            Assert.AreEqual(layer.ChunkExtent, restored.ChunkExtent);
            AssertSameChunks(layer.Chunks, restored.Chunks);
        }

        [Test]
        public void TheFixturePlaceLayerReadFromItsStoreSurvivesARoundTrip() {
            var place = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(FixturePlacePath);

            if (place == null) {
                Assert.Ignore("the fixture Place " + FixturePlacePath + " is not in this project");
            }

            var layer = FirstVoxelLayer(place);

            Assert.IsNotNull(layer);
            Assert.IsNotNull(layer.Store);
            Assert.AreEqual(FixtureRunCount, CountRuns(layer.Chunks));

            var restored = RoundTrip(layer);

            Assert.IsNull(restored.Store);
            Assert.AreEqual(layer.ChunkSize, restored.ChunkSize);
            Assert.AreEqual(layer.ChunkExtent, restored.ChunkExtent);
            Assert.AreEqual(FixtureRunCount, CountRuns(restored.Chunks));
            AssertSameChunks(layer.Chunks, restored.Chunks);
        }

        [Test]
        public void ATruncatedPackDoesNotUnpack() {
            var layer = VoxelLayerOwnerRoundTripTests.CreateLayer(Vector3Int.one,
                new VoxelLayerChunk(Vector3Int.zero, VoxelLayerOwnerRoundTripTests.CanonicalRuns()));
            var bytes = Pack(layer);

            Assert.IsFalse(CoreKit.Packing.TryUnpack(bytes.AsSpan(0, bytes.Length - 1), out VoxelLayer _));
        }

        private static VoxelLayer RoundTrip(VoxelLayer layer) {
            var bytes = Pack(layer);

            Assert.IsTrue(CoreKit.Packing.TryUnpack(bytes, out VoxelLayer restored));

            return restored;
        }

        private static byte[] Pack(VoxelLayer layer) {
            var buffer = new byte[64 + 16 * layer.Chunks.Count + 6 * CountRuns(layer.Chunks)];

            Assert.IsTrue(CoreKit.Packing.TryPack(layer, buffer, out var written));

            return buffer.AsSpan(0, written).ToArray();
        }

        private static VoxelLayer FirstVoxelLayer(PlaceDefinition place) {
            foreach (var placeLayer in place.Layers) {
                if (placeLayer is VoxelLayer voxelLayer) {
                    return voxelLayer;
                }
            }

            return null;
        }

        private static int CountRuns(IReadOnlyList<VoxelLayerChunk> chunks) {
            var count = 0;

            foreach (var chunk in chunks) {
                count += chunk.Runs.Count;
            }

            return count;
        }

        private static void AssertSameChunks(IReadOnlyList<VoxelLayerChunk> expected,
            IReadOnlyList<VoxelLayerChunk> actual) {
            Assert.AreEqual(expected.Count, actual.Count);

            for (var chunkIndex = 0; chunkIndex < expected.Count; chunkIndex++) {
                Assert.AreEqual(expected[chunkIndex].Offset, actual[chunkIndex].Offset, "chunk " + chunkIndex);

                var expectedRuns = expected[chunkIndex].Runs;
                var actualRuns = actual[chunkIndex].Runs;

                Assert.AreEqual(expectedRuns.Count, actualRuns.Count, "chunk " + chunkIndex);

                for (var runIndex = 0; runIndex < expectedRuns.Count; runIndex++) {
                    if (expectedRuns[runIndex].StartIndex != actualRuns[runIndex].StartIndex
                        || expectedRuns[runIndex].Voxel != actualRuns[runIndex].Voxel) {
                        Assert.Fail("chunk " + chunkIndex + " run " + runIndex + " differs after the round trip");
                    }
                }
            }
        }
    }
}
