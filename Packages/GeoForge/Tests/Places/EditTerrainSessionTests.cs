// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places.Tests {
    public class EditTerrainSessionTests {
        private const int ChunkSize = 32;
        private const string LayerName = "voxels";
        private const string PointsLayerName = "deposits";
        private const float CarveSize = 8f;
        private const float FillSize = 10f;
        private const int FirstOpenRow = 8;
        private const sbyte Solid = sbyte.MaxValue;
        private const sbyte Empty = sbyte.MinValue;

        private static readonly Vector3 Centre = Vector3.one * (ChunkSize / 2f);
        private static readonly Vector3 OpenPoint = new(ChunkSize / 2f, 20f, ChunkSize / 2f);

        private GeoForgePlacesTestFolder _folder;
        private EditTerrainSession _session;

        [SetUp]
        public void CreateFolderAndSavedScene() {
            _folder = new GeoForgePlacesTestFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, _folder.Path("Host.unity"));
        }

        [TearDown]
        public void CancelSessionAndDeleteFolder() {
            if (_session != null && _session.IsOpen) {
                _session.Cancel();
            }

            _session = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _folder.Dispose();
        }

        [Test]
        public void ARemoveSphereClosesIntoTheSameLayerAndTheScratchSceneIsGone() {
            var place = CreatePlace("RoundTrip", LayerOfSolidRows(DataWidth()));
            var layer = (VoxelLayer)place.Layers[0];
            var runsBefore = layer.Chunks[0].Runs.ToList();
            var scenesBefore = SceneManager.sceneCount;

            _session = EditTerrainSession.Open(place, 0);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore + 1));
            Assert.That(_session.Brush, Is.Not.Null);
            Assert.That(_session.Brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, Centre,
                CarveSize, 0)), Is.True);
            Assert.That(_session.Volume.TryQuerySolid(Centre, out var solid), Is.True);
            Assert.That(solid, Is.False);

            var report = new ValidationReport(place.name);
            Assert.That(_session.TryClose(report), Is.True, report.ToText());

            Assert.That(_session.IsOpen, Is.False);
            Assert.That(place.Layers[0], Is.SameAs(layer));
            Assert.That(layer.Chunks[0].Runs.ToList(), Is.Not.EqualTo(runsBefore));
            var store = AssetDatabase.LoadAssetAtPath<VoxelRunStore>(StorePath(place));
            Assert.That(store.Chunks[0].Runs.ToList(), Is.EqualTo(layer.Chunks[0].Runs.ToList()));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore));
            Assert.That(Object.FindObjectsByType<GeoForgeManager>(FindObjectsInactive.Include), Is.Empty);
        }

        [Test]
        public void ASealedPlacementHoldsTheCaptureAndKeepsTheStrokesUntilUndone() {
            var place = CreatePlace("Sealed", LayerOfSolidRows(FirstOpenRow));
            place.AddLayer(new PlacementPointsTestLayer { LayerName = PointsLayerName }
                .AddPoint(PointsLayerName + "[0]", OpenPoint));
            Save(place);
            var runsBefore = ((VoxelLayer)place.Layers[0]).Chunks[0].Runs.ToList();

            _session = EditTerrainSession.Open(place, 0);
            Assert.That(_session.Brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Add, OpenPoint,
                FillSize, 0)), Is.True);

            var sealedReport = new ValidationReport(place.name);
            Assert.That(_session.TryClose(sealedReport), Is.False);

            Assert.That(sealedReport.Findings.Select(finding => finding.Code),
                Has.Member(GeoForgePlacesFindingCodes.InsideRock));
            Assert.That(sealedReport.Findings.Select(finding => finding.Code), Has.Member("geoforge.inside-rock"));
            Assert.That(_session.IsOpen, Is.True);
            Assert.That(_session.Scene.IsValid() && _session.Scene.isLoaded, Is.True);
            Assert.That(_session.Brush.Strokes.Count, Is.EqualTo(1));
            Assert.That(_session.SealedPlacements, Is.EqualTo(new[] { PointsLayerName + "[0]" }));
            Assert.That(((VoxelLayer)place.Layers[0]).Chunks[0].Runs.ToList(), Is.EqualTo(runsBefore));

            Assert.That(_session.Brush.Undo(), Is.True);
            var openReport = new ValidationReport(place.name);
            Assert.That(_session.TryClose(openReport), Is.True, openReport.ToText());
            Assert.That(_session.IsOpen, Is.False);
        }

        [Test]
        public void CancelLeavesThePlaceAndItsStoreUnchanged() {
            var place = CreatePlace("Cancelled", LayerOfSolidRows(DataWidth()));
            var placePath = AssetDatabase.GetAssetPath(place);
            var storePath = StorePath(place);
            var placeHash = HashOf(placePath);
            var storeHash = HashOf(storePath);
            var scenesBefore = SceneManager.sceneCount;

            _session = EditTerrainSession.Open(place, 0);
            Assert.That(_session.Brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, Centre,
                CarveSize, 0)), Is.True);
            _session.Cancel();
            AssetDatabase.SaveAssets();

            Assert.That(_session.IsOpen, Is.False);
            Assert.That(HashOf(placePath), Is.EqualTo(placeHash));
            Assert.That(HashOf(storePath), Is.EqualTo(storeHash));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore));
        }

        [Test]
        public void ThePreviewRootLivesInTheScratchScene() {
            var place = CreatePlace("Previewed", LayerOfSolidRows(DataWidth()));

            _session = EditTerrainSession.Open(place, 0);

            Assert.That(_session.PreviewRoot, Is.Not.Null);
            Assert.That(_session.PreviewRoot.scene, Is.EqualTo(_session.Scene));
            Assert.That(_session.StampReport.HasErrors, Is.False, _session.StampReport.ToText());
            Assert.That(_session.Volume.TryQuerySolid(Centre, out var solid), Is.True);
            Assert.That(solid, Is.True);
        }

        [Test]
        public void OpeningBesideAnUntitledSceneIsRefused() {
            var placePath = AssetDatabase.GetAssetPath(CreatePlace("Untitled", LayerOfSolidRows(DataWidth())));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var place = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(placePath);
            var scenesBefore = SceneManager.sceneCount;

            Assert.Throws<InvalidOperationException>(() => EditTerrainSession.Open(place, 0));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore));
        }

        [Test]
        public void OpeningALayerThatIsNotAVoxelLayerIsRefused() {
            var place = CreatePlace("NotVoxel", LayerOfSolidRows(DataWidth()));
            place.AddLayer(new PlacementPointsTestLayer { LayerName = PointsLayerName });

            Assert.Throws<ArgumentException>(() => EditTerrainSession.Open(place, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EditTerrainSession.Open(place, 2));
        }

        private PlaceDefinition CreatePlace(string placeName, VoxelLayer layer) {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            AssetDatabase.CreateAsset(place, _folder.Path(placeName + ".asset"));
            VoxelRunStores.Write(place, null, layer);
            Save(place);

            return place;
        }

        private static void Save(PlaceDefinition place) {
            EditorUtility.SetDirty(place);
            AssetDatabase.SaveAssets();
        }

        private static string StorePath(PlaceDefinition place) {
            return VoxelRunStores.PathBeside(AssetDatabase.GetAssetPath(place), LayerName);
        }

        private static int DataWidth() {
            return ChunkLayout.DataWidth(ChunkSize);
        }

        private static VoxelLayer LayerOfSolidRows(int firstOpenRow) {
            var width = DataWidth();
            var runs = new List<SparseVoxelData> { new(VoxelData.CreateMature(Solid, 1), 0) };

            if (firstOpenRow < width) {
                runs.Add(new SparseVoxelData(VoxelData.CreateImmature(Empty, 0), firstOpenRow * width * width));
            }

            var layer = new VoxelLayer { LayerName = LayerName };
            layer.SetChunks(ChunkSize, Vector3Int.one, new[] { new VoxelLayerChunk(Vector3Int.zero, runs) });

            return layer;
        }

        private static string HashOf(string assetPath) {
            using var algorithm = SHA256.Create();

            return Convert.ToBase64String(algorithm.ComputeHash(File.ReadAllBytes(assetPath)));
        }
    }
}
