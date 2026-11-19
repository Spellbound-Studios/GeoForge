// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Authoring.Editor;
using Spellbound.Core.Ground;
using Spellbound.Core.Observations;
using Spellbound.Core.Operations;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class GeoForgeLayerEditOperationTests {
        private const int ChunkSize = 32;
        private const string LayerName = "voxels";
        private const sbyte Solid = sbyte.MaxValue;

        private static readonly Vector3 Centre = Vector3.one * (ChunkSize / 2f);

        private static readonly string AtTheCentre =
            string.Format(CultureInfo.InvariantCulture, " --x {0} --y {1} --z {2}", Centre.x, Centre.y, Centre.z);

        private static readonly string RemoveAtTheCentre = " --mode remove --shape sphere" + AtTheCentre + " --size 8";

        private GeoForgePlacesTestFolder _folder;
        private bool _draftsFolderExisted;
        private string _draftsFolder;
        private readonly List<string> _draftPaths = new();

        [SetUp]
        public void CreateFolder() {
            _folder = new GeoForgePlacesTestFolder();
            _draftsFolder = DefinitionDrafts.Folder(CoreKit.Operations.Settings);
            _draftsFolderExisted = AssetDatabase.IsValidFolder(_draftsFolder);
        }

        [TearDown]
        public void DeleteDraftsAndFolder() {
            foreach (var draftPath in _draftPaths) {
                AssetDatabase.DeleteAsset(draftPath);
            }

            _draftPaths.Clear();

            if (!_draftsFolderExisted && AssetDatabase.IsValidFolder(_draftsFolder)
                && AssetDatabase.FindAssets(string.Empty, new[] { _draftsFolder }).Length == 0) {
                AssetDatabase.DeleteAsset(_draftsFolder);
            }

            _folder.Dispose();
        }

        [Test]
        public void TheOperationIsLayerEditAndWrites() {
            var method = typeof(GeoForgeLayerEditOperation).GetMethod(nameof(GeoForgeLayerEditOperation.LayerEdit));
            var attribute = (OperationAttribute)Attribute.GetCustomAttribute(method, typeof(OperationAttribute));

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.Name, Is.EqualTo("layer-edit"));
            Assert.That(attribute.Effect, Is.EqualTo(OperationEffect.Writes));
        }

        [Test]
        public void ARemoveSphereGivesTheRevisionItsOwnStoreAndLeavesTheOriginalUnchanged() {
            var original = CreatePlace("Carved");
            var originalStorePath = StorePath(AssetDatabase.GetAssetPath(original));
            var originalStoreHash = HashOf(originalStorePath);
            var revisionPath = Revise(original);
            var revisionStorePath = StorePath(revisionPath);

            var result = Run("geoforge layer-edit --target asset:" + revisionPath + RemoveAtTheCentre);

            Assert.That(result.Status, Is.EqualTo(OperationStatus.Succeeded), result.Text);
            StringAssert.StartsWith("succeeded:", result.Text);
            StringAssert.Contains(LayerName, result.Text);
            StringAssert.Contains("remove sphere", result.Text);
            Assert.That(AssetDatabase.LoadAssetAtPath<VoxelRunStore>(revisionStorePath), Is.Not.Null);
            var revision = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(revisionPath);
            var revisedLayer = (VoxelLayer)revision.FindLayer(LayerName);
            Assert.That(AssetDatabase.GetAssetPath(revisedLayer.Store), Is.EqualTo(revisionStorePath));
            Assert.That(OccupancyAtTheCentre(revisedLayer), Is.Not.EqualTo(Occupancy.Solid));
            Assert.That(HashOf(originalStorePath), Is.EqualTo(originalStoreHash));
            Assert.That(OccupancyAtTheCentre((VoxelLayer)original.FindLayer(LayerName)), Is.EqualTo(Occupancy.Solid));
        }

        [Test]
        public void ADryRunAnswersPreviewedNamingTheStrokeAndWritesNothing() {
            var original = CreatePlace("Previewed");
            var originalStorePath = StorePath(AssetDatabase.GetAssetPath(original));
            var originalStoreHash = HashOf(originalStorePath);
            var revisionPath = Revise(original);
            var revisionHash = HashOf(revisionPath);

            var result = Run("geoforge layer-edit --target asset:" + revisionPath + RemoveAtTheCentre + " --dry-run");

            Assert.That(result.Status, Is.EqualTo(OperationStatus.Previewed), result.Text);
            StringAssert.StartsWith("previewed:", result.Text);
            StringAssert.Contains("remove sphere", result.Text);
            AssetDatabase.SaveAssets();
            Assert.That(File.Exists(StorePath(revisionPath)), Is.False);
            Assert.That(HashOf(revisionPath), Is.EqualTo(revisionHash));
            Assert.That(HashOf(originalStorePath), Is.EqualTo(originalStoreHash));
        }

        [Test]
        public void ATargetOutsideTheDraftsFolderIsInvalid() {
            var original = CreatePlace("Approved");
            var originalPath = AssetDatabase.GetAssetPath(original);
            var originalStoreHash = HashOf(StorePath(originalPath));

            var result = Run("geoforge layer-edit --target asset:" + originalPath + RemoveAtTheCentre);

            Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid), result.Text);
            StringAssert.StartsWith("invalid:", result.Text);
            StringAssert.Contains(_draftsFolder, result.Text);
            Assert.That(HashOf(StorePath(originalPath)), Is.EqualTo(originalStoreHash));
        }

        [Test]
        public void ARevisionWithoutAVoxelLayerOfThatNameIsInvalid() {
            var revisionPath = Revise(CreatePlace("Unnamed"));

            var result = Run("geoforge layer-edit --target asset:" + revisionPath + RemoveAtTheCentre
                + " --layer-name tunnels");

            Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid), result.Text);
            StringAssert.StartsWith("invalid:", result.Text);
            StringAssert.Contains("tunnels", result.Text);
            Assert.That(File.Exists(StorePath(revisionPath)), Is.False);
        }

        [Test]
        public void AnUnknownModeIsRefusedBeforeAnyWrite() {
            var revisionPath = Revise(CreatePlace("Unknown"));

            var result = Run("geoforge layer-edit --target asset:" + revisionPath
                + " --mode melt --shape sphere" + AtTheCentre + " --size 8");

            Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid), result.Text);
            StringAssert.StartsWith("invalid:", result.Text);
            StringAssert.Contains("melt", result.Text);
            Assert.That(File.Exists(StorePath(revisionPath)), Is.False);
        }

        private PlaceDefinition CreatePlace(string placeName) {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            var uniqueName = "LayerEdit" + placeName + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateAsset(place, _folder.Path(uniqueName + ".asset"));
            VoxelRunStores.Write(place, null, SolidLayer());
            EditorUtility.SetDirty(place);
            AssetDatabase.SaveAssets();

            return place;
        }

        private string Revise(PlaceDefinition original) {
            var revisionPath = DefinitionDrafts.PathFor(original.name + DefinitionDrafts.RevisionSuffix,
                CoreKit.Operations.Settings);
            _draftPaths.Add(revisionPath);
            _draftPaths.Add(StorePath(revisionPath));

            var result = Run("core revise --target asset:" + AssetDatabase.GetAssetPath(original));

            Assert.That(result.Succeeded, Is.True, result.Text);
            Assert.That(AssetDatabase.LoadAssetAtPath<PlaceDefinition>(revisionPath), Is.Not.Null);

            return revisionPath;
        }

        private static VoxelLayer SolidLayer() {
            var runs = new List<SparseVoxelData> { new(VoxelData.CreateMature(Solid, 1), 0) };
            var layer = new VoxelLayer { LayerName = LayerName };
            layer.SetChunks(ChunkSize, Vector3Int.one, new[] { new VoxelLayerChunk(Vector3Int.zero, runs) });

            return layer;
        }

        private static Occupancy OccupancyAtTheCentre(VoxelLayer layer) {
            using var source = new VoxelLayerOccupancySource(layer);

            return source.Classify(new Bounds(Centre, Vector3.one));
        }

        private static string StorePath(string placePath) {
            return VoxelRunStores.PathBeside(placePath, LayerName);
        }

        private static string HashOf(string assetPath) {
            using var algorithm = SHA256.Create();

            return Convert.ToBase64String(algorithm.ComputeHash(File.ReadAllBytes(assetPath)));
        }

        private static OperationResult Run(string line) {
            var run = CoreKit.Operations.Run(line, OperationCaller.Development(), ObservationContext.Discard);
            Assert.That(run.IsDone, Is.True);

            return run.Result;
        }
    }
}
