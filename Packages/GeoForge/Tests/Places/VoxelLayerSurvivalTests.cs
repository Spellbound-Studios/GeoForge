// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelLayerSurvivalTests {
        private GeoForgePlacesTestFolder _folder;

        [SetUp]
        public void CreateFolder() {
            _folder = new GeoForgePlacesTestFolder();
        }

        [TearDown]
        public void DeleteFolder() {
            _folder.Dispose();
        }

        [Test]
        public void SetChunksRefusesANullChunk() {
            var layer = new VoxelLayer { LayerName = "voxels" };

            Assert.Throws<ArgumentException>(() =>
                layer.SetChunks(64, Vector3Int.one, new VoxelLayerChunk[] { null }));
            Assert.AreEqual(0, layer.Chunks.Count);
        }

        [Test]
        public void AVoxelLayerSurvivesASaveAndAForcedReimport() {
            var path = _folder.Path("VoxelPlace.asset");
            var runs = new[] {
                new SparseVoxelData(VoxelData.CreateMature(127, 1), 0),
                new SparseVoxelData(VoxelData.CreateImmature(-128, 0), 1000),
                new SparseVoxelData(VoxelData.CreateMature(127, 2), 300762)
            };
            var layer = new VoxelLayer { LayerName = "voxels" };
            layer.SetChunks(64, Vector3Int.one, new[] { new VoxelLayerChunk(Vector3Int.zero, runs) });
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            place.AddLayer(layer);
            AssetDatabase.CreateAsset(place, path);
            AssetDatabase.SaveAssets();

            var saved = File.ReadAllText(path);
            StringAssert.Contains("asm: Spellbound.GeoForge.Places", saved);
            StringAssert.Contains("class: VoxelLayer", saved);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var restored = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(path);

            Assert.IsNotNull(restored, path);
            Assert.IsFalse(SerializationUtility.HasManagedReferencesWithMissingTypes(restored));
            Assert.IsInstanceOf<VoxelLayer>(restored.Layers[0]);
            var restoredLayer = (VoxelLayer)restored.Layers[0];
            Assert.AreEqual("voxels", restoredLayer.LayerName);
            Assert.AreEqual(64, restoredLayer.ChunkSize);
            Assert.AreEqual(Vector3Int.one, restoredLayer.ChunkExtent);
            Assert.AreEqual(1, restoredLayer.Chunks.Count);
            Assert.AreEqual(Vector3Int.zero, restoredLayer.Chunks[0].Offset);
            var restoredRuns = restoredLayer.Chunks[0].Runs;
            Assert.AreEqual(runs.Length, restoredRuns.Count);

            for (var index = 0; index < runs.Length; index++) {
                Assert.AreEqual(runs[index].Voxel.Density, restoredRuns[index].Voxel.Density, "run " + index);
                Assert.AreEqual(runs[index].Voxel.MaterialIndex, restoredRuns[index].Voxel.MaterialIndex,
                    "run " + index);
                Assert.AreEqual(runs[index].StartIndex, restoredRuns[index].StartIndex, "run " + index);
            }
        }

        [Test]
        public void TheDataWidthAndVolumeFollowTheChunkSize() {
            var layer = new VoxelLayer();

            Assert.AreEqual(64, layer.ChunkSize);
            Assert.AreEqual(Vector3Int.one, layer.ChunkExtent);
            Assert.AreEqual(0, layer.Chunks.Count);
            Assert.AreEqual(67, layer.DataWidth);
            Assert.AreEqual(67 * 67 * 67, layer.ChunkDataVolume);

            layer.SetChunks(16, new Vector3Int(2, 1, 1),
                new[] { new VoxelLayerChunk(), new VoxelLayerChunk(Vector3Int.right, Array.Empty<SparseVoxelData>()) });

            Assert.AreEqual(16, layer.ChunkSize);
            Assert.AreEqual(new Vector3Int(2, 1, 1), layer.ChunkExtent);
            Assert.AreEqual(2, layer.Chunks.Count);
            Assert.AreEqual(19, layer.DataWidth);
            Assert.AreEqual(19 * 19 * 19, layer.ChunkDataVolume);
        }

        [Test]
        public void SetChunksRefusesANullListAndASizeOrExtentBelowOne() {
            var layer = new VoxelLayer();
            var chunks = new[] { new VoxelLayerChunk() };

            Assert.Throws<ArgumentNullException>(() => layer.SetChunks(64, Vector3Int.one, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => layer.SetChunks(0, Vector3Int.one, chunks));
            Assert.Throws<ArgumentOutOfRangeException>(() => layer.SetChunks(64, new Vector3Int(1, 0, 1), chunks));
        }

        [Test]
        public void AChunkCopiesTheRunsItIsGiven() {
            var runs = new List<SparseVoxelData> { new(VoxelData.CreateMature(127, 1), 0) };
            var chunk = new VoxelLayerChunk(new Vector3Int(1, 2, 3), runs);
            runs.Add(new SparseVoxelData(VoxelData.CreateMature(127, 2), 10));

            Assert.AreEqual(new Vector3Int(1, 2, 3), chunk.Offset);
            Assert.AreEqual(1, chunk.Runs.Count);

            var empty = new VoxelLayerChunk();

            Assert.AreEqual(Vector3Int.zero, empty.Offset);
            Assert.AreEqual(0, empty.Runs.Count);
            Assert.Throws<ArgumentNullException>(() => new VoxelLayerChunk(Vector3Int.zero, null));
        }
    }
}
