// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places.Tests {
    public class GeoForgePlacesKitTests {
        private const float SurfaceHeight = 20f;

        private BridgeTestWorld _world;
        private PlaceDefinition _place;

        [SetUp]
        public void CreateWorld() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
            _place = ScriptableObject.CreateInstance<PlaceDefinition>();
        }

        [TearDown]
        public void DisposeWorld() {
            _world.Dispose();
            Object.DestroyImmediate(_place);
        }

        [Test]
        public void StampThroughTheKitWritesTheLayer() {
            var runs = VoxelLayerOwnerRoundTripTests.CanonicalRuns();
            _place.AddLayer(VoxelLayerOwnerRoundTripTests.CreateLayer(Vector3Int.one,
                new VoxelLayerChunk(Vector3Int.zero, runs)));
            var report = new ValidationReport("stamp");

            var stamped = GeoForgePlacesKit.Stamp(_place, _world.Volume, VoxelRegion.Origin, report);

            Assert.IsTrue(stamped, report.ToText());
            Assert.IsTrue(report.IsEmpty, report.ToText());
            VoxelLayerOwnerRoundTripTests.AssertSameVoxels(runs, _world.RunsOf(Vector3Int.zero), _world.DataVolume);
            Assert.Throws<ArgumentNullException>(() =>
                GeoForgePlacesKit.Stamp(null, _world.Volume, VoxelRegion.Origin, report));
            Assert.Throws<ArgumentNullException>(() =>
                GeoForgePlacesKit.Stamp(_place, null, VoxelRegion.Origin, report));
            Assert.Throws<ArgumentNullException>(() =>
                GeoForgePlacesKit.Stamp(_place, _world.Volume, VoxelRegion.Origin, null));
        }

        [Test]
        public void CaptureThroughTheKitReturnsTheLayerNamed() {
            var report = new ValidationReport("capture");

            var captured = GeoForgePlacesKit.Capture(_world.Volume, VoxelRegion.Origin, "terrain", report);

            Assert.IsTrue(report.IsEmpty, report.ToText());
            Assert.IsNotNull(captured);
            Assert.AreEqual("terrain", captured.LayerName);
            Assert.AreEqual(1, captured.Chunks.Count);
            VoxelLayerOwnerRoundTripTests.AssertSameVoxels(_world.RunsOf(Vector3Int.zero), captured.Chunks[0].Runs,
                _world.DataVolume);
            Assert.Throws<ArgumentNullException>(() =>
                GeoForgePlacesKit.Capture(null, VoxelRegion.Origin, "terrain", report));
            Assert.Throws<ArgumentNullException>(() =>
                GeoForgePlacesKit.Capture(_world.Volume, VoxelRegion.Origin, "terrain", null));
        }
    }
}
