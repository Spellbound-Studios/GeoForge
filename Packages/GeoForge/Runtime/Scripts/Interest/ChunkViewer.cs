// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public struct ChunkViewer {
        public Vector3 Position;
        public float CollisionRadius;
        public float DetailRadius;

        public ChunkViewer(Vector3 position, float collisionRadius, float detailRadius) {
            Position = position;
            CollisionRadius = collisionRadius;
            DetailRadius = detailRadius;
        }
    }
}
