// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Places.Owners;

namespace Spellbound.GeoForge.Places {
    [PlaceLayerOwner]
    public sealed class VoxelLayerOwner : IPlaceLayerOwner<VoxelLayer> {
        private const string VolumeMissingMessage = "no GeoForge volume with a running engine is the owner's target";

        public VoxelLayerOwner() {
            Region = VoxelRegion.Origin;
        }

        public VoxelLayerOwner(IGeoVolume volume, VoxelRegion region) {
            Volume = volume ?? throw new ArgumentNullException(nameof(volume));
            Region = region;
        }

        public IGeoVolume Volume { get; }

        public VoxelRegion Region { get; }

        public void Stamp(VoxelLayer layer, PlaceStampContext context) {
            if (layer == null) {
                throw new ArgumentNullException(nameof(layer));
            }

            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (Volume?.GeoVolumeEngine == null) {
                context.Report.Error(GeoForgePlacesFindingCodes.VolumeMissing,
                    "voxel layer '" + layer.LayerName + "' is not stamped: " + VolumeMissingMessage, layer.LayerName);
            }
        }

        public VoxelLayer Capture(PlaceCaptureContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (Volume?.GeoVolumeEngine == null) {
                context.Report.Error(GeoForgePlacesFindingCodes.VolumeMissing,
                    "voxel layer '" + context.LayerName + "' is not captured: " + VolumeMissingMessage,
                    context.LayerName);
            }

            return null;
        }
    }
}
