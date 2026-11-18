// Copyright (c) Spellbound Studios. All rights reserved.

using System.IO;
using NUnit.Framework;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelRunStoreTests {
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
        public void ALayerReadingAStoreSurvivesASaveAndAReimport() {
            var placePath = _folder.Path("StoredPlace.asset");
            var storePath = _folder.Path("StoredPlaceRuns.asset");
            var runs = VoxelLayerOwnerRoundTripTests.CanonicalRuns();
            var chunk = new VoxelLayerChunk(Vector3Int.zero, runs);

            var store = ScriptableObject.CreateInstance<VoxelRunStore>();
            store.SetChunks(new[] { chunk });
            AssetDatabase.CreateAsset(store, storePath);

            var layer = VoxelLayerOwnerRoundTripTests.CreateLayer(Vector3Int.one, chunk);
            layer.UseStore(store);
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            place.AddLayer(layer);
            AssetDatabase.CreateAsset(place, placePath);
            AssetDatabase.SaveAssets();

            var placeText = File.ReadAllText(placePath);
            StringAssert.DoesNotContain("startIndex", placeText);
            StringAssert.Contains("store: {fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(storePath),
                placeText);
            StringAssert.Contains("startIndex", File.ReadAllText(storePath));

            AssetDatabase.ImportAsset(storePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(placePath, ImportAssetOptions.ForceUpdate);
            var restored = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(placePath);

            Assert.IsNotNull(restored, placePath);
            Assert.IsFalse(SerializationUtility.HasManagedReferencesWithMissingTypes(restored));

            var restoredLayer = (VoxelLayer)restored.Layers[0];

            Assert.IsNotNull(restoredLayer.Store);
            Assert.AreEqual(storePath, AssetDatabase.GetAssetPath(restoredLayer.Store));
            Assert.AreEqual(BridgeTestWorld.ChunkSize, restoredLayer.ChunkSize);
            Assert.AreEqual(1, restoredLayer.Chunks.Count);
            Assert.AreEqual(Vector3Int.zero, restoredLayer.Chunks[0].Offset);

            var restoredRuns = restoredLayer.Chunks[0].Runs;

            Assert.AreEqual(runs.Count, restoredRuns.Count);

            for (var index = 0; index < runs.Count; index++) {
                Assert.AreEqual(runs[index].Voxel, restoredRuns[index].Voxel, "run " + index);
                Assert.AreEqual(runs[index].StartIndex, restoredRuns[index].StartIndex, "run " + index);
            }

            restoredLayer.SetChunks(BridgeTestWorld.ChunkSize, Vector3Int.one, new[] { chunk });

            Assert.IsNull(restoredLayer.Store);
            Assert.AreEqual(1, restoredLayer.Chunks.Count);
        }
    }
}
