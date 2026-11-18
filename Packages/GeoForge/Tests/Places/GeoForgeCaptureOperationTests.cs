// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Observations;
using Spellbound.Core.Operations;
using Spellbound.Places;
using Spellbound.Places.Arrangement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class GeoForgeCaptureOperationTests {
        private const float SurfaceHeight = 20f;
        private const float RaisedSurfaceHeight = 40f;
        private const string LayerName = "voxels";

        private GeoForgePlacesTestFolder _folder;
        private BridgeTestWorld _world;

        [SetUp]
        public void CreateFolder() {
            _folder = new GeoForgePlacesTestFolder();
        }

        [TearDown]
        public void DisposeWorldAndFolder() {
            _world?.Dispose();
            _world = null;
            _folder.Dispose();
        }

        [Test]
        public void ACaptureWritesTheLayerAndAnswersItsCounts() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            var path = CreatePlace("Captured").Path;

            var result = Run("geoforge capture --target asset:" + path + " --layer-name " + LayerName);

            Assert.IsTrue(result.Succeeded, result.Text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(path);
            Assert.AreEqual(1, reloaded.Layers.Count);
            var layer = reloaded.Layers[0] as VoxelLayer;
            Assert.IsNotNull(layer);
            Assert.AreEqual(LayerName, layer.LayerName);
            Assert.AreEqual(1, layer.Chunks.Count);
            var runCount = layer.Chunks[0].Runs.Count;
            Assert.GreaterOrEqual(runCount, 1);
            StringAssert.Contains(LayerName, result.Text);
            StringAssert.Contains("1 chunk", result.Text);
            StringAssert.Contains(runCount + " runs", result.Text);
        }

        [Test]
        public void ACaptureIntoAnExistingLayerRewritesItInPlace() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            var created = CreatePlace("Recaptured");
            var line = "geoforge capture --target asset:" + created.Path + " --layer-name " + LayerName;

            var first = Run(line);
            Assert.IsTrue(first.Succeeded, first.Text);
            var firstLayer = created.Place.Layers[0];
            var firstStarts = StartIndices((VoxelLayer)firstLayer);
            _world.Dispose();
            _world = new BridgeTestWorld(RaisedSurfaceHeight, Vector3Int.zero);
            var second = Run(line);

            Assert.IsTrue(second.Succeeded, second.Text);
            Assert.AreEqual(1, created.Place.Layers.Count);
            Assert.AreSame(firstLayer, created.Place.Layers[0]);
            Assert.IsInstanceOf<VoxelLayer>(created.Place.Layers[0]);
            var secondLayer = (VoxelLayer)created.Place.Layers[0];
            Assert.AreEqual(1, secondLayer.Chunks.Count);
            CollectionAssert.AreNotEqual(firstStarts, StartIndices(secondLayer));
        }

        [Test]
        public void ACaptureIntoALayerReadingAStoreWritesTheStore() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            var storePath = _folder.Path("StoredRuns.asset");
            var store = VoxelRunStoreTests.CreateStore(storePath);
            var layer = VoxelLayerOwnerRoundTripTests.CreateLayer(Vector3Int.one, store.Chunks[0]);
            layer.UseStore(store);
            var path = CreatePlace("Stored", layer).Path;

            var result = Run("geoforge capture --target asset:" + path + " --layer-name " + LayerName);

            Assert.IsTrue(result.Succeeded, result.Text);
            Assert.AreEqual(storePath, AssetDatabase.GetAssetPath(Reload(path).Store));
            StringAssert.DoesNotContain("startIndex", File.ReadAllText(path));
            StringAssert.Contains("startIndex", File.ReadAllText(storePath));
            var storedRuns = AssetDatabase.LoadAssetAtPath<VoxelRunStore>(storePath).Chunks[0].Runs.Count;
            Assert.AreEqual(_world.RunsOf(Vector3Int.zero).Count, storedRuns);
            StringAssert.Contains(storedRuns + " runs", result.Text);
        }

        [Test]
        public void ACaptureIntoANewLayerMakesAStoreBesideThePlace() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            var path = CreatePlace("Fresh").Path;
            var storePath = VoxelRunStores.PathBeside(path, LayerName);

            var result = Run("geoforge capture --target asset:" + path + " --layer-name " + LayerName);

            Assert.IsTrue(result.Succeeded, result.Text);
            Assert.IsTrue(File.Exists(storePath), storePath);
            Assert.AreEqual(storePath, AssetDatabase.GetAssetPath(Reload(path).Store));
            StringAssert.DoesNotContain("startIndex", File.ReadAllText(path));
        }

        [Test]
        public void ADryRunAnswersPreviewedAndWritesNothing() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            var created = CreatePlace("Previewed");
            var bytesBefore = File.ReadAllBytes(created.Path);

            var result = Run("geoforge capture --target asset:" + created.Path + " --layer-name " + LayerName
                + " --dry-run");

            StringAssert.StartsWith("previewed:", result.Text);
            CollectionAssert.AreEqual(bytesBefore, File.ReadAllBytes(created.Path));
            Assert.AreEqual(0, created.Place.Layers.Count);
            Assert.IsFalse(File.Exists(VoxelRunStores.PathBeside(created.Path, LayerName)));
        }

        [Test]
        public void ACaptureWithNoVolumeInTheSceneIsInvalid() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsNull(SceneVolumes.FirstWithEngine());
            var created = CreatePlace("Unvolumed");

            var result = Run("geoforge capture --target asset:" + created.Path + " --layer-name " + LayerName);

            StringAssert.StartsWith("invalid:", result.Text);
            StringAssert.Contains("volume", result.Text);
            Assert.AreEqual(0, created.Place.Layers.Count);
        }

        [Test]
        public void ACaptureOverALayerOfAnotherTypeIsInvalid() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            var created = CreatePlace("Arranged", new ArrangementLayer { LayerName = LayerName });

            var result = Run("geoforge capture --target asset:" + created.Path + " --layer-name " + LayerName);

            StringAssert.StartsWith("invalid:", result.Text);
            StringAssert.Contains(nameof(ArrangementLayer), result.Text);
            Assert.AreEqual(1, created.Place.Layers.Count);
            Assert.IsInstanceOf<ArrangementLayer>(created.Place.Layers[0]);
        }

        private (PlaceDefinition Place, string Path) CreatePlace(string placeName, params PlaceLayer[] layers) {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();

            foreach (var layer in layers) {
                place.AddLayer(layer);
            }

            var path = _folder.Path(placeName + ".asset");
            AssetDatabase.CreateAsset(place, path);
            AssetDatabase.SaveAssets();

            return (place, path);
        }

        private static VoxelLayer Reload(string path) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<PlaceDefinition>(path).FindLayer(LayerName) as VoxelLayer;
        }

        private static List<int> StartIndices(VoxelLayer layer) {
            var starts = new List<int>();

            foreach (var run in layer.Chunks[0].Runs) {
                starts.Add(run.StartIndex);
            }

            return starts;
        }

        private static OperationResult Run(string line) {
            var run = CoreKit.Operations.Run(line, OperationCaller.Development(), ObservationContext.Discard);
            Assert.IsTrue(run.IsDone);

            return run.Result;
        }
    }
}
