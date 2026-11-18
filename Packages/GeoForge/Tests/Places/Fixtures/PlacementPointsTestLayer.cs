// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    [Serializable]
    public sealed class PlacementPointsTestLayer : PlaceLayer {
        [SerializeField] private List<string> locations = new();
        [SerializeField] private List<Vector3> positions = new();

        public PlacementPointsTestLayer AddPoint(string location, Vector3 position) {
            locations.Add(location);
            positions.Add(position);
            return this;
        }

        public override void Validate(ValidationReport report, string location) {
        }

        public override IEnumerable<PlacementPoint> PlacementPoints() {
            for (var index = 0; index < locations.Count; index++) {
                yield return new PlacementPoint(LayerName, locations[index], positions[index]);
            }
        }
    }
}
