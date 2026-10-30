// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IChunkPresenter {
        IChunkLeaf Acquire(Transform parent, int levelOfDetail);

        void Release(IChunkLeaf leaf);
    }
}
