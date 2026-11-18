// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Authoring;
using Spellbound.Core.Authoring.Editor;
using Spellbound.Core.Ground;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [RegisteredDefinitionCheck(GeoForgePlacesCheckNames.PlacementsInRock, RunsWhileEditing = true)]
    public sealed class PlacementsInRockCheck : IDefinitionCheck<PlaceDefinition> {
        public const float DefaultPlacementBoxSize = 1.5f;

        private readonly float _placementBoxSize = DefaultPlacementBoxSize;
        private readonly Vector3 _worldOrigin = Vector3.zero;
        private readonly float _voxelSize = 1f;

        public PlacementsInRockCheck() { }

        public PlacementsInRockCheck(float placementBoxSize, Vector3 worldOrigin, float voxelSize) {
            if (placementBoxSize <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(placementBoxSize), placementBoxSize,
                    "A placement box has a positive size.");
            }

            if (voxelSize <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(voxelSize), voxelSize, "A voxel has a positive size.");
            }

            _placementBoxSize = placementBoxSize;
            _worldOrigin = worldOrigin;
            _voxelSize = voxelSize;
        }

        public void Check(PlaceDefinition definition, ValidationReport report) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            var layer = FirstVoxelLayer(definition);

            if (layer == null) {
                return;
            }

            using var source = new VoxelLayerOccupancySource(layer, _worldOrigin, _voxelSize);
            var boxSize = Vector3.one * _placementBoxSize;

            foreach (var point in definition.PlacementPoints) {
                if (source.Classify(new Bounds(point.Position, boxSize)) != Occupancy.Solid) {
                    continue;
                }

                report.Error(GeoForgePlacesFindingCodes.InsideRock,
                    "placement '" + point.Location + "' of layer '" + point.LayerName + "' is inside rock",
                    point.Location, "move it or carve to it");
            }
        }

        private static VoxelLayer FirstVoxelLayer(PlaceDefinition definition) {
            foreach (var layer in definition.Layers) {
                if (layer is VoxelLayer voxelLayer) {
                    return voxelLayer;
                }
            }

            return null;
        }
    }
}
