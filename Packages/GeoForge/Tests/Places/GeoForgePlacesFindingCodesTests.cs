// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class GeoForgePlacesFindingCodesTests {
        [TestCase(GeoForgePlacesFindingCodes.VoxelLayerEmpty, "geoforge.voxel-layer-empty")]
        [TestCase(GeoForgePlacesFindingCodes.VoxelLayerChunksMismatch, "geoforge.voxel-layer-chunks-mismatch")]
        [TestCase(GeoForgePlacesFindingCodes.RunOutOfRange, "geoforge.run-out-of-range")]
        [TestCase(GeoForgePlacesFindingCodes.VolumeMissing, "geoforge.volume-missing")]
        [TestCase(GeoForgePlacesFindingCodes.ChunkNotLoaded, "geoforge.chunk-not-loaded")]
        [TestCase(GeoForgePlacesFindingCodes.InsideRock, "geoforge.inside-rock")]
        [TestCase(GeoForgePlacesFindingCodes.VoxelLayerInReferenceFrame, "geoforge.voxel-layer-in-reference-frame")]
        public void EachCodeReadsAsWritten(string code, string expected) {
            Assert.AreEqual(expected, code);
        }

        [Test]
        public void EveryCodePassesCoresCodeRule() {
            var codes = typeof(GeoForgePlacesFindingCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue())
                .ToList();

            Assert.AreEqual(7, codes.Count);
            Assert.AreEqual(codes.Count, codes.Distinct().Count());

            foreach (var code in codes) {
                Assert.IsTrue(ValidationFinding.IsCode(code), code);
                StringAssert.StartsWith("geoforge.", code);
            }
        }

        [Test]
        public void AnEmptyVoxelLayerReportsVoxelLayerEmpty() {
            var report = new ValidationReport("place");

            new VoxelLayer { LayerName = "voxels" }.Validate(report, "layers[0]");

            Assert.AreEqual(1, report.Findings.Count, report.ToText());
            Assert.AreEqual(GeoForgePlacesFindingCodes.VoxelLayerEmpty, report.Findings[0].Code);
            Assert.AreEqual("layers[0]", report.Findings[0].Location);
            Assert.AreEqual(FindingSeverity.Error, report.Findings[0].Severity);
        }

        [Test]
        public void AChunkCountOtherThanTheExtentReportsVoxelLayerChunksMismatch() {
            var layer = new VoxelLayer { LayerName = "voxels" };
            layer.SetChunks(64, new Vector3Int(2, 1, 1), new[] { new VoxelLayerChunk() });
            var report = new ValidationReport("place");

            layer.Validate(report, "layers[0]");

            Assert.AreEqual(1, report.Findings.Count, report.ToText());
            Assert.AreEqual(GeoForgePlacesFindingCodes.VoxelLayerChunksMismatch, report.Findings[0].Code);
            Assert.AreEqual("layers[0]", report.Findings[0].Location);
        }

        [Test]
        public void ARunOutsideTheChunkReportsRunOutOfRange() {
            var layer = new VoxelLayer { LayerName = "voxels" };
            var inside = new SparseVoxelData(VoxelData.CreateMature(127, 1), 0);
            var below = new SparseVoxelData(VoxelData.CreateMature(127, 1), -1);
            var past = new SparseVoxelData(VoxelData.CreateMature(127, 1), layer.ChunkDataVolume);
            layer.SetChunks(64, new Vector3Int(1, 1, 2), new[] {
                new VoxelLayerChunk(Vector3Int.zero, new[] { inside }),
                new VoxelLayerChunk(Vector3Int.forward, new[] { inside, below, past })
            });
            var report = new ValidationReport("place");

            layer.Validate(report, "layers[0]");

            Assert.AreEqual(2, report.Findings.Count, report.ToText());
            Assert.IsTrue(report.Findings.All(finding => finding.Code == GeoForgePlacesFindingCodes.RunOutOfRange));
            Assert.IsTrue(report.Findings.All(finding => finding.Location == "layers[0].chunks[1]"));
        }

        [Test]
        public void AWellFormedVoxelLayerReportsNothingAndANullReportThrows() {
            var layer = new VoxelLayer { LayerName = "voxels" };
            layer.SetChunks(64, Vector3Int.one, new[] {
                new VoxelLayerChunk(Vector3Int.zero,
                    new[] { new SparseVoxelData(VoxelData.CreateMature(127, 1), layer.ChunkDataVolume - 1) })
            });
            var report = new ValidationReport("place");

            layer.Validate(report, "layers[0]");

            Assert.IsTrue(report.IsEmpty, report.ToText());
            Assert.Throws<ArgumentNullException>(() => layer.Validate(null, "layers[0]"));
        }
    }
}
