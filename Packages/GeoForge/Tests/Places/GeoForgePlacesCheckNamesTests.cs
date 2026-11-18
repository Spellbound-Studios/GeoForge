// Copyright (c) Spellbound Studios. All rights reserved.

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spellbound.Core.Authoring;

namespace Spellbound.GeoForge.Places.Tests {
    public class GeoForgePlacesCheckNamesTests {
        [Test]
        public void PlacementsInRockReadsAsWritten() {
            Assert.AreEqual("geoforge.placements-in-rock", GeoForgePlacesCheckNames.PlacementsInRock);
        }

        [Test]
        public void EveryCheckNamePassesCoresCodeRule() {
            var names = typeof(GeoForgePlacesCheckNames).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue())
                .ToList();

            Assert.AreEqual(1, names.Count);

            foreach (var name in names) {
                Assert.IsTrue(ValidationFinding.IsCode(name), name);
                StringAssert.StartsWith("geoforge.", name);
            }
        }
    }
}
