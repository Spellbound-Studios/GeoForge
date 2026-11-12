// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Observations;

namespace Spellbound.GeoForge.Networking {
    public static class GeoForgeNetworkingKit {
        public static VoxelEditStores CreateEditStores(IGeoVolume volume, in VoxelEditSourceSettings settings,
            ObservationContext observations) {
            if (volume == null)
                throw new ArgumentNullException(nameof(volume));

            if (observations == null)
                throw new ArgumentNullException(nameof(observations));

            return new VoxelEditStores(volume, settings, observations);
        }
    }
}