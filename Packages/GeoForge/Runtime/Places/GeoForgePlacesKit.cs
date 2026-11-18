// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using Spellbound.Places.Owners;

namespace Spellbound.GeoForge.Places {
    public static class GeoForgePlacesKit {
        public static bool Stamp(PlaceDefinition place, IGeoVolume volume, VoxelRegion region,
            ValidationReport report) {
            if (place == null) {
                throw new ArgumentNullException(nameof(place));
            }

            if (volume == null) {
                throw new ArgumentNullException(nameof(volume));
            }

            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            var registry = new PlaceLayerOwnerRegistry(new object[] { new VoxelLayerOwner(volume, region) });

            return PlacesKit.Stamp(place, new PlaceStampContext(report, owners: registry));
        }

        public static VoxelLayer Capture(IGeoVolume volume, VoxelRegion region, string layerName,
            ValidationReport report) {
            if (volume == null) {
                throw new ArgumentNullException(nameof(volume));
            }

            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            return new VoxelLayerOwner(volume, region).Capture(new PlaceCaptureContext(layerName, report));
        }
    }
}
