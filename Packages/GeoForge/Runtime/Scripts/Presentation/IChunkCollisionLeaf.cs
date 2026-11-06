// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IChunkCollisionLeaf {
        bool HasMesh { get; }

        void SetMesh(Mesh mesh);

        void SetVolumeMatrix(Matrix4x4 worldToLocal);
    }
}
