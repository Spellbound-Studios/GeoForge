// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Spellbound.GeoForge.Places;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace PlacesConversion.Tests {
    public sealed class ConvertedPlaceTests {
        private const string ConvertedPlacePath = "Assets/_Project/PlacesConversion/MainShaftBottomV2.asset";
        private const int CaptureRunCount = 124718;
        private const int CaptureDataWidth = 67;

        private const string SingleRunCaptureText = "  dimensions: {x: 67, y: 67, z: 67}\n"
                                                    + "  sparseVoxelData:\n"
                                                    + "  - Voxel:\n"
                                                    + "      Density: -12\n"
                                                    + "      MaterialIndex: 130\n"
                                                    + "    startIndex: 42\n";

        [Test]
        public void TheConvertedPlaceHoldsTheCapturesRuns() {
            var place = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(ConvertedPlacePath);

            Assert.IsNotNull(place);
            Assert.AreEqual(1, place.Layers.Count);

            var layer = place.Layers[0] as VoxelLayer;

            Assert.IsNotNull(layer);
            Assert.AreEqual("voxels", layer.LayerName);
            Assert.AreEqual(64, layer.ChunkSize);
            Assert.AreEqual(Vector3Int.one, layer.ChunkExtent);
            Assert.AreEqual(CaptureDataWidth, layer.DataWidth);
            Assert.AreEqual(1, layer.Chunks.Count);
            Assert.AreEqual(Vector3Int.zero, layer.Chunks[0].Offset);
            Assert.AreEqual(CaptureRunCount, layer.Chunks[0].Runs.Count);

            var firstRun = layer.Chunks[0].Runs[0];

            Assert.AreEqual(127, firstRun.Voxel.Density);
            Assert.AreEqual(128, firstRun.Voxel.MaterialIndex);
            Assert.AreEqual(0, firstRun.StartIndex);
            Assert.IsFalse(SerializationUtility.HasManagedReferencesWithMissingTypes(place));
        }

        [Test]
        public void TheCaptureTextParserReadsAChunk() {
            var result = CaptureText.Parse(SingleRunCaptureText);

            Assert.AreEqual(new Vector3Int(CaptureDataWidth, CaptureDataWidth, CaptureDataWidth), result.Dimensions);
            Assert.AreEqual(1, result.Runs.Count);

            var chunk = new VoxelLayerChunk(Vector3Int.zero, result.Runs);

            Assert.AreEqual(1, chunk.Runs.Count);
            Assert.AreEqual(-12, chunk.Runs[0].Voxel.Density);
            Assert.AreEqual(130, chunk.Runs[0].Voxel.MaterialIndex);
            Assert.AreEqual(42, chunk.Runs[0].StartIndex);
        }
    }
}