// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using Spellbound.Places.Owners;
using Unity.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelLayerOwnerRoundTripTests {
        private const string LayerName = "voxels";
        private const float SurfaceHeight = 20f;

        private readonly List<BridgeTestWorld> _worlds = new();
        private readonly List<Object> _created = new();

        [TearDown]
        public void DisposeWorlds() {
            foreach (var world in _worlds) {
                world.Dispose();
            }

            _worlds.Clear();

            foreach (var created in _created) {
                if (created != null) {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void StampThenCaptureGivesTheSameVoxels() {
            var world = CreateWorld(Vector3Int.zero);
            var layer = CreateLayer(Vector3Int.one, new VoxelLayerChunk(Vector3Int.zero, CanonicalRuns()));
            var owner = new VoxelLayerOwner(world.Volume, VoxelRegion.Origin);
            var stampReport = new ValidationReport("stamp");

            owner.Stamp(layer, new PlaceStampContext(stampReport));

            Assert.IsTrue(stampReport.IsEmpty, stampReport.ToText());

            var captureReport = new ValidationReport("capture");
            var captured = owner.Capture(new PlaceCaptureContext(LayerName, captureReport));

            Assert.IsTrue(captureReport.IsEmpty, captureReport.ToText());
            Assert.IsNotNull(captured);
            Assert.AreEqual(LayerName, captured.LayerName);
            Assert.AreEqual(BridgeTestWorld.ChunkSize, captured.ChunkSize);
            Assert.AreEqual(1, captured.Chunks.Count);
            Assert.AreEqual(300763, world.DataVolume);

            AssertSameVoxels(layer.Chunks[0].Runs, captured.Chunks[0].Runs, world.DataVolume);
        }

        [Test]
        public void CaptureOfTwoChunksHasTwoChunksWithTheirOffsets() {
            var world = CreateWorld(Vector3Int.zero, Vector3Int.right);
            var region = new VoxelRegion(Vector3Int.zero, new Vector3Int(2, 1, 1));
            var report = new ValidationReport("capture");

            var owner = new VoxelLayerOwner(world.Volume, region);

            var captured = owner.Capture(new PlaceCaptureContext(LayerName, report));

            Assert.IsTrue(report.IsEmpty, report.ToText());
            Assert.AreEqual(new Vector3Int(2, 1, 1), captured.ChunkExtent);
            Assert.AreEqual(2, captured.Chunks.Count);
            Assert.AreEqual(Vector3Int.zero, captured.Chunks[0].Offset);
            Assert.AreEqual(Vector3Int.right, captured.Chunks[1].Offset);
            AssertSameVoxels(world.RunsOf(Vector3Int.zero), captured.Chunks[0].Runs, world.DataVolume);
            AssertSameVoxels(world.RunsOf(Vector3Int.right), captured.Chunks[1].Runs, world.DataVolume);

            var validation = new ValidationReport("validate");
            captured.Validate(validation, "layers[0]");

            Assert.IsTrue(validation.IsEmpty, validation.ToText());
        }

        [Test]
        public void StampIntoAVolumeMissingAChunkReportsChunkNotLoaded() {
            var world = CreateWorld(Vector3Int.zero);
            var runs = CanonicalRuns();
            var layer = CreateLayer(new Vector3Int(2, 1, 1), new VoxelLayerChunk(Vector3Int.zero, runs),
                new VoxelLayerChunk(Vector3Int.right, runs));
            var report = new ValidationReport("stamp");

            new VoxelLayerOwner(world.Volume, VoxelRegion.Origin).Stamp(layer, new PlaceStampContext(report));

            Assert.AreEqual(1, report.Findings.Count, report.ToText());
            Assert.AreEqual(GeoForgePlacesFindingCodes.ChunkNotLoaded, report.Findings[0].Code);
            Assert.AreEqual(FindingSeverity.Error, report.Findings[0].Severity);
            Assert.AreEqual(LayerName + ".chunks[1]", report.Findings[0].Location);
            AssertSameVoxels(runs, world.RunsOf(Vector3Int.zero), world.DataVolume);
        }

        [Test]
        public void AStampUnderPreviewWritesNothing() {
            var world = CreateWorld(Vector3Int.zero);
            var before = world.RunsOf(Vector3Int.zero);
            var layer = CreateLayer(Vector3Int.one, new VoxelLayerChunk(Vector3Int.zero, CanonicalRuns()));
            var report = new ValidationReport("stamp");

            new VoxelLayerOwner(world.Volume, VoxelRegion.Origin).Stamp(layer,
                new PlaceStampContext(report, preview: true));

            Assert.IsTrue(report.IsEmpty, report.ToText());

            var after = world.RunsOf(Vector3Int.zero);

            Assert.AreEqual(before.Count, after.Count);

            for (var index = 0; index < before.Count; index++) {
                Assert.AreEqual(before[index].StartIndex, after[index].StartIndex, "run " + index);
                Assert.AreEqual(before[index].Voxel, after[index].Voxel, "run " + index);
            }
        }

        [Test]
        public void TheParameterlessOwnerWithNoVolumeInTheSceneReportsVolumeMissing() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var place = CreatePlace(CreateLayer(Vector3Int.one,
                new VoxelLayerChunk(Vector3Int.zero, CanonicalRuns())));
            var report = new ValidationReport("stamp");

            var stamped = PlacesKit.Stamp(place, new PlaceStampContext(report));

            Assert.IsFalse(stamped);
            Assert.AreEqual(1, report.Findings.Count, report.ToText());
            Assert.AreEqual(GeoForgePlacesFindingCodes.VolumeMissing, report.Findings[0].Code);
            Assert.AreEqual(LayerName, report.Findings[0].Location);
        }

        public static List<SparseVoxelData> CanonicalRuns() {
            return new List<SparseVoxelData> {
                new(VoxelData.CreateMature(127, 1), 0),
                new(VoxelData.CreateImmature(127, 1), 4489),
                new(VoxelData.CreateImmature(40, 2), 90000),
                new(VoxelData.CreateMature(-20, 2), 150381),
                new(VoxelData.CreateImmature(-128, 0), 151000),
                new(VoxelData.CreateMature(-128, 1), 300000)
            };
        }

        public static VoxelLayer CreateLayer(Vector3Int extent, params VoxelLayerChunk[] chunks) {
            var layer = new VoxelLayer { LayerName = LayerName };
            layer.SetChunks(BridgeTestWorld.ChunkSize, extent, chunks);

            return layer;
        }

        public static void AssertSameVoxels(IReadOnlyList<SparseVoxelData> expectedRuns,
            IReadOnlyList<SparseVoxelData> actualRuns, int dataVolume) {
            using var expected = VoxelRunDecoder.Decode(expectedRuns, dataVolume, Allocator.Temp);
            using var actual = VoxelRunDecoder.Decode(actualRuns, dataVolume, Allocator.Temp);

            Assert.AreEqual(dataVolume, expected.Length);
            Assert.AreEqual(dataVolume, actual.Length);

            for (var index = 0; index < dataVolume; index++) {
                if (expected[index].Density != actual[index].Density
                    || expected[index].MaterialIndex != actual[index].MaterialIndex) {
                    Assert.Fail("voxel " + index + " reads " + actual[index].Density + "/"
                                + actual[index].MaterialIndex + " where " + expected[index].Density + "/"
                                + expected[index].MaterialIndex + " was stamped");
                }
            }
        }

        private BridgeTestWorld CreateWorld(params Vector3Int[] chunkCoordinates) {
            var world = new BridgeTestWorld(SurfaceHeight, chunkCoordinates);
            _worlds.Add(world);

            return world;
        }

        private PlaceDefinition CreatePlace(VoxelLayer layer) {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            _created.Add(place);
            place.AddLayer(layer);

            return place;
        }
    }
}
