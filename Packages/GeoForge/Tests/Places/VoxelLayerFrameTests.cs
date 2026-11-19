// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.Core.ReferenceFrames;
using Spellbound.Places;
using Spellbound.Places.Owners;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelLayerFrameTests {
        private const float SurfaceHeight = 20f;

        private readonly List<Object> _created = new();
        private BridgeTestWorld _world;

        [SetUp]
        public void CreateWorld() {
            _world = new BridgeTestWorld(SurfaceHeight, Vector3Int.zero);
        }

        [TearDown]
        public void DisposeWorld() {
            _world.Dispose();

            foreach (var created in _created) {
                if (created != null) {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AStampIntoAReferenceFrameIsRefusedAndWritesNothing() {
            var before = _world.RunsOf(Vector3Int.zero);
            var place = CreatePlace();
            var report = new ValidationReport("stamp");

            var stamped = PlacesKit.Stamp(place, new PlaceStampContext(report, new ReferenceFrameId(7),
                owners: CreateRegistry()));

            Assert.IsFalse(stamped);
            Assert.AreEqual(1, report.Findings.Count, report.ToText());
            Assert.AreEqual(GeoForgePlacesFindingCodes.VoxelLayerInReferenceFrame, report.Findings[0].Code);
            Assert.AreEqual(FindingSeverity.Error, report.Findings[0].Severity);
            Assert.AreEqual("voxels", report.Findings[0].Location);
            StringAssert.Contains(new ReferenceFrameId(7).ToString(), report.Findings[0].Message);
            VoxelLayerOwnerRoundTripTests.AssertSameVoxels(before, _world.RunsOf(Vector3Int.zero),
                _world.DataVolume);
        }

        [Test]
        public void AStampIntoTheWorldFrameStillWrites() {
            var place = CreatePlace();
            var report = new ValidationReport("stamp");

            var stamped = PlacesKit.Stamp(place, new PlaceStampContext(report, owners: CreateRegistry()));

            Assert.IsTrue(stamped, report.ToText());
            Assert.IsTrue(report.IsEmpty, report.ToText());
            VoxelLayerOwnerRoundTripTests.AssertSameVoxels(VoxelLayerOwnerRoundTripTests.CanonicalRuns(),
                _world.RunsOf(Vector3Int.zero), _world.DataVolume);
        }

        private PlaceLayerOwnerRegistry CreateRegistry() {
            return new PlaceLayerOwnerRegistry(new object[] { new VoxelLayerOwner(_world.Volume, VoxelRegion.Origin) });
        }

        private PlaceDefinition CreatePlace() {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            _created.Add(place);
            place.AddLayer(VoxelLayerOwnerRoundTripTests.CreateLayer(Vector3Int.one,
                new VoxelLayerChunk(Vector3Int.zero, VoxelLayerOwnerRoundTripTests.CanonicalRuns())));

            return place;
        }
    }
}
