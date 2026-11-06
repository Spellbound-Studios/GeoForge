// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IChunkCollisionPresenter {
        bool BakesMeshesBeforeApply { get; }

        IChunkCollisionLeaf Acquire(Transform parent, int levelOfDetail);

        void Release(IChunkCollisionLeaf leaf);
    }
}
