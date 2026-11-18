// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Places;
using Spellbound.Places.Editor;

namespace Spellbound.GeoForge.Places {
    [PlaceLayerInspectorAction]
    public sealed class EditTerrainInspectorActions : IPlaceLayerInspectorActions {
        public const string EditTerrainLabel = "Edit terrain";

        public Type LayerType => typeof(VoxelLayer);

        public float VoxelSize { get; set; } = EditTerrainSession.DefaultVoxelSize;

        public IReadOnlyList<PlaceLayerInspectorAction> Actions(PlaceDefinition place, int layerIndex) {
            return new[] { new PlaceLayerInspectorAction(EditTerrainLabel, OpenSession) };
        }

        private void OpenSession(PlaceDefinition place, int layerIndex) {
            EditTerrainWindow.Open(EditTerrainSession.Open(place, layerIndex, VoxelSize));
        }
    }
}
