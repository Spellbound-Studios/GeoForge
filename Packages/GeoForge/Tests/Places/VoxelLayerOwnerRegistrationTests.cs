// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Linq;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.GeoForge.Tests;
using Spellbound.Places;
using Spellbound.Places.Owners;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelLayerOwnerRegistrationTests {
        [TearDown]
        public void ResetRegistry() {
            PlaceLayerOwnerRegistry.Reset();
        }

        [Test]
        public void TheDefaultRegistryHasTheVoxelLayer() {
            PlaceLayerOwnerRegistry.Reset();

            Assert.IsTrue(PlaceLayerOwnerRegistry.Default.Has(typeof(VoxelLayer)));
        }

        [Test]
        public void StampOfAnOwnerWithoutAVolumeReportsVolumeMissing() {
            PlaceLayerOwnerRegistry.Reset();
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();

            try {
                var layer = new VoxelLayer { LayerName = "voxels" };
                layer.SetChunks(64, Vector3Int.one, new[] { new VoxelLayerChunk() });
                place.AddLayer(layer);
                var report = new ValidationReport("place");

                var stamped = PlacesKit.Stamp(place, new PlaceStampContext(report));

                Assert.IsFalse(stamped, report.ToText());
                Assert.IsTrue(report.Findings.Any(finding => finding.Code == GeoForgePlacesFindingCodes.VolumeMissing),
                    report.ToText());
                Assert.IsFalse(report.Findings.Any(finding => finding.Code == PlaceFindingCodes.LayerNotInstalled),
                    report.ToText());
            } finally {
                UnityEngine.Object.DestroyImmediate(place);
            }
        }

        [Test]
        public void TheParameterlessOwnerHasNoVolumeAndTheOriginRegion() {
            var owner = new VoxelLayerOwner();

            Assert.IsNull(owner.Volume);
            Assert.AreEqual(VoxelRegion.Origin, owner.Region);
        }

        [Test]
        public void TheExplicitOwnerKeepsItsVolumeAndRegionAndRefusesANullVolume() {
            var gameObject = new GameObject("GeoForgePlacesVolume");

            try {
                var volume = gameObject.AddComponent<GeoVolumeDouble>();
                var region = new VoxelRegion(new Vector3Int(1, 2, 3), new Vector3Int(2, 1, 2));

                var owner = new VoxelLayerOwner(volume, region);

                Assert.AreSame(volume, owner.Volume);
                Assert.AreEqual(region, owner.Region);
                Assert.Throws<ArgumentNullException>(() => new VoxelLayerOwner(null, region));
            } finally {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StampAndCaptureRefuseAVolumeWithoutAnEngine() {
            var gameObject = new GameObject("GeoForgePlacesVolume");

            try {
                var owner = new VoxelLayerOwner(gameObject.AddComponent<GeoVolumeDouble>(), VoxelRegion.Origin);
                var stampReport = new ValidationReport("place");
                var captureReport = new ValidationReport("place");

                owner.Stamp(new VoxelLayer { LayerName = "voxels" }, new PlaceStampContext(stampReport));
                var captured = owner.Capture(new PlaceCaptureContext("voxels", captureReport));

                Assert.AreEqual(1, stampReport.Findings.Count, stampReport.ToText());
                Assert.AreEqual(GeoForgePlacesFindingCodes.VolumeMissing, stampReport.Findings[0].Code);
                Assert.AreEqual("voxels", stampReport.Findings[0].Location);
                Assert.AreEqual(FindingSeverity.Error, stampReport.Findings[0].Severity);
                Assert.IsNull(captured);
                Assert.IsTrue(
                    captureReport.Findings.Any(finding => finding.Code == GeoForgePlacesFindingCodes.VolumeMissing),
                    captureReport.ToText());
            } finally {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StampAndCaptureRefuseNullArguments() {
            var owner = new VoxelLayerOwner();
            var context = new PlaceStampContext(new ValidationReport("place"));

            Assert.Throws<ArgumentNullException>(() => owner.Stamp(null, context));
            Assert.Throws<ArgumentNullException>(() => owner.Stamp(new VoxelLayer(), null));
            Assert.Throws<ArgumentNullException>(() => owner.Capture(null));
        }

        [Test]
        public void TheOwnerImplementsOneOwnerInterface() {
            var interfaces = typeof(VoxelLayerOwner).GetInterfaces();

            Assert.AreEqual(1, interfaces.Length);
            Assert.AreEqual(typeof(IPlaceLayerOwner<VoxelLayer>), interfaces[0]);
        }
    }
}
