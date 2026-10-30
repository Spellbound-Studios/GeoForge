// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IChunkLeaf {
        Transform Transform { get; }

        string Name { get; set; }

        void SetMesh(Mesh mesh, int levelOfDetail);

        void SetTransitionMesh(Mesh mesh);

        void SetVolumeMatrix(Matrix4x4 worldToLocal);

        void SetVisible(bool visible);
    }
}
