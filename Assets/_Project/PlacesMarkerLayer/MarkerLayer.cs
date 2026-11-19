// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.PlacesMarker {
    [Serializable]
    public sealed class MarkerLayer : PlaceLayer {
        public Vector3 point;

        public override void Validate(ValidationReport report, string location) {
        }

        public override IEnumerable<PlacementPoint> PlacementPoints() {
            yield return new PlacementPoint(LayerName, LayerName + "[0]", point);
        }
    }
}
