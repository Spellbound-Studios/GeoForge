// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [Serializable]
    public struct VoxelStroke {
        [SerializeField] private VoxelStrokeShape shape;
        [SerializeField] private VoxelStrokeMode mode;
        [SerializeField] private Vector3 centre;
        [SerializeField] private float size;
        [SerializeField] private byte material;

        public VoxelStroke(VoxelStrokeShape shape, VoxelStrokeMode mode, Vector3 centre, float size, byte material) {
            this.shape = shape;
            this.mode = mode;
            this.centre = centre;
            this.size = size;
            this.material = material;
        }

        public VoxelStrokeShape Shape => shape;

        public VoxelStrokeMode Mode => mode;

        public Vector3 Centre => centre;

        public float Size => size;

        public byte Material => material;
    }
}
