// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Observations;
using Spellbound.Core.Operations;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class GeoForgeBakeOperationTests {
        private const string LayerName = "voxels";
        private const int ChunkSize = 64;

        private GeoForgePlacesTestFolder _folder;
        private string _generatorPath;
        private string _configPath;

        [SetUp]
        public void CreateFolderAndSources() {
            _folder = new GeoForgePlacesTestFolder();
            _generatorPath = _folder.Path("Generator.asset");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PerlinTerrainDataFactory>(), _generatorPath);
            _configPath = _folder.Path("Config.asset");
            AssetDatabase.CreateAsset(CreateConfig(), _configPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void DisposeFolder() {
            _folder.Dispose();
        }

        [Test]
        public void ABakeFillsTheLayerAndWritesTheReport() {
            var path = CreatePlace("Baked");

            var result = Run(BakeLine(path));

            Assert.IsTrue(result.Succeeded, result.Text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(path);
            var layer = reloaded.FindLayer(LayerName) as VoxelLayer;
            Assert.IsNotNull(layer);
            Assert.AreEqual(1, layer.Chunks.Count);
            var runCount = layer.Chunks[0].Runs.Count;
            Assert.GreaterOrEqual(runCount, 1);
            var reportPath = ReportPath(path);
            Assert.IsTrue(File.Exists(reportPath), reportPath);
            var lines = File.ReadAllLines(reportPath);
            StringAssert.StartsWith("place: ", lines[0]);
            CollectionAssert.Contains(lines, "runs: " + runCount);
        }

        [Test]
        public void ADryRunAnswersPreviewedAndWritesNoReport() {
            var path = CreatePlace("Previewed");
            var bytesBefore = File.ReadAllBytes(path);

            var result = Run(BakeLine(path) + " --dry-run");

            StringAssert.StartsWith("previewed:", result.Text);
            CollectionAssert.AreEqual(bytesBefore, File.ReadAllBytes(path));
            Assert.IsFalse(File.Exists(ReportPath(path)));
        }

        [Test]
        public void ABakeWithoutAGeneratorIsInvalid() {
            var path = CreatePlace("Ungenerated");

            var result = Run("geoforge bake --target asset:" + path + " --config asset:" + _configPath);

            StringAssert.StartsWith("invalid:", result.Text);
            StringAssert.Contains("--generator", result.Text);
            Assert.IsFalse(File.Exists(ReportPath(path)));
        }

        [Test]
        public void ABakeOverATwoChunkLayerFillsBothChunks() {
            var layer = new VoxelLayer { LayerName = LayerName };
            layer.SetChunks(ChunkSize, new Vector3Int(2, 1, 1),
                new List<VoxelLayerChunk> { new(), new() });
            var path = CreatePlace("Widened", layer);

            var result = Run(BakeLine(path));

            Assert.IsTrue(result.Succeeded, result.Text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(path);
            Assert.AreEqual(1, reloaded.Layers.Count);
            var baked = reloaded.FindLayer(LayerName) as VoxelLayer;
            Assert.IsNotNull(baked);
            Assert.AreEqual(new Vector3Int(2, 1, 1), baked.ChunkExtent);
            Assert.AreEqual(2, baked.Chunks.Count);
            Assert.AreEqual(Vector3Int.zero, baked.Chunks[0].Offset);
            Assert.AreEqual(Vector3Int.right, baked.Chunks[1].Offset);

            foreach (var chunk in baked.Chunks) {
                Assert.GreaterOrEqual(chunk.Runs.Count, 1);
            }

            CollectionAssert.Contains(File.ReadAllLines(ReportPath(path)), "chunks: 2");
        }

        private string BakeLine(string placePath) {
            return "geoforge bake --target asset:" + placePath + " --generator asset:" + _generatorPath
                   + " --config asset:" + _configPath;
        }

        private string CreatePlace(string placeName, params PlaceLayer[] layers) {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();

            foreach (var layer in layers) {
                place.AddLayer(layer);
            }

            var path = _folder.Path(placeName + ".asset");
            AssetDatabase.CreateAsset(place, path);
            AssetDatabase.SaveAssets();

            return path;
        }

        private static VoxelVolumeConfig CreateConfig() {
            var config = ScriptableObject.CreateInstance<VoxelVolumeConfig>();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("cubesPerMarch").intValue = 16;
            serialized.FindProperty("levelsOfDetail").intValue = 3;
            serialized.FindProperty("maxChunkSize").intValue = ChunkSize;
            serialized.FindProperty("chunkSize").intValue = ChunkSize;
            serialized.FindProperty("resolution").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return config;
        }

        private static string ReportPath(string placePath) {
            return Path.ChangeExtension(placePath, null) + ".bake.txt";
        }

        private static OperationResult Run(string line) {
            var run = CoreKit.Operations.Run(line, OperationCaller.Development(), ObservationContext.Discard);
            Assert.IsTrue(run.IsDone);

            return run.Result;
        }
    }
}
