// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public sealed class VoxelStrokeHolder : ScriptableObject {
        [SerializeField] private VoxelStroke stroke;

        public VoxelStroke Stroke {
            get => stroke;
            set => stroke = value;
        }
    }
}
