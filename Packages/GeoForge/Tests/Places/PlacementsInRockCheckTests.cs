// Copyright (c) Spellbound Studios. All rights reserved.

using System.Linq;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.Core.Authoring.Editor;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class PlacementsInRockCheckTests {
        private PlaceDefinition _place;

        [TearDown]
        public void DestroyPlace() {
            if (_place != null) {
                Object.DestroyImmediate(_place);
            }
        }

        [Test]
        public void APointInRockIsReportedAndAPointInAirIsNot() {
            _place = Place(new PlacementPointsTestLayer { LayerName = "deposits" }
                .AddPoint("deposits[0]", new Vector3(5f, 2f, 5f))
                .AddPoint("deposits[1]", new Vector3(5f, 8f, 5f)), HalfSolidVoxelLayer.Create());

            var report = Check(_place);

            Assert.AreEqual(1, report.Findings.Count, report.ToText());
            var finding = report.Findings.Single();
            Assert.AreEqual(FindingSeverity.Error, finding.Severity);
            Assert.AreEqual(GeoForgePlacesFindingCodes.InsideRock, finding.Code);
            Assert.AreEqual("geoforge.inside-rock", finding.Code);
            Assert.AreEqual("deposits[0]", finding.Location);
            Assert.AreEqual("move it or carve to it", finding.Hint);
        }

        [Test]
        public void APointAgainstTheWallIsNotReported() {
            _place = Place(new PlacementPointsTestLayer { LayerName = "deposits" }
                .AddPoint("deposits[0]", new Vector3(5f, 4.5f, 5f)), HalfSolidVoxelLayer.Create());

            var report = Check(_place);

            Assert.AreEqual(0, report.Findings.Count, report.ToText());
        }

        [Test]
        public void APlaceWithNoVoxelLayerYieldsNothing() {
            _place = Place(new PlacementPointsTestLayer { LayerName = "deposits" }
                .AddPoint("deposits[0]", new Vector3(5f, 2f, 5f)));

            var report = Check(_place);

            Assert.AreEqual(0, report.Findings.Count, report.ToText());
        }

        [Test]
        public void TheBoxSizeOriginAndVoxelSizeAreTheChecksOwn() {
            _place = Place(new PlacementPointsTestLayer { LayerName = "deposits" }
                .AddPoint("deposits[0]", new Vector3(5f, 3.5f, 5f))
                .AddPoint("deposits[1]", new Vector3(5f, 102f, 5f)), HalfSolidVoxelLayer.Create());

            var standard = Check(_place);
            var wide = new ValidationReport("place");
            new PlacementsInRockCheck(3.5f, Vector3.zero, 1f).Check(_place, wide);
            var raised = new ValidationReport("place");
            new PlacementsInRockCheck(3.5f, new Vector3(0f, 100f, 0f), 1f).Check(_place, raised);

            Assert.AreEqual(1, standard.Findings.Count, standard.ToText());
            Assert.AreEqual("deposits[0]", standard.Findings.Single().Location);
            Assert.AreEqual(0, wide.Findings.Count, wide.ToText());
            Assert.AreEqual(1, raised.Findings.Count, raised.ToText());
            Assert.AreEqual("deposits[1]", raised.Findings.Single().Location);
        }

        [Test]
        public void TheCheckNamePassesCoresCodeRule() {
            Assert.IsTrue(ValidationFinding.IsCode(GeoForgePlacesCheckNames.PlacementsInRock));
        }

        [Test]
        public void TheRegistryListsTheCheck() {
            var check = DefinitionCheckRegistry.Default.Find(GeoForgePlacesCheckNames.PlacementsInRock);

            Assert.IsNotNull(check);
            Assert.AreEqual(typeof(PlaceDefinition), check.DefinitionType);
            Assert.AreEqual(typeof(PlacementsInRockCheck), check.CheckType);
            Assert.IsTrue(check.RunsWhileEditing);
            CollectionAssert.Contains(DefinitionCheckRegistry.Default.For(typeof(PlaceDefinition)), check);
        }

        private static PlaceDefinition Place(params PlaceLayer[] layers) {
            var place = ScriptableObject.CreateInstance<PlaceDefinition>();

            foreach (var layer in layers) {
                place.AddLayer(layer);
            }

            return place;
        }

        private static ValidationReport Check(PlaceDefinition place) {
            var report = new ValidationReport("place");
            new PlacementsInRockCheck().Check(place, report);
            return report;
        }
    }
}
