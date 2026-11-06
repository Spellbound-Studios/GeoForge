// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class AcquireThrowingChunkPresenter : IChunkPresenter {
        internal RecordingChunkPresenter Recording { get; } = new();

        internal bool ThrowsOnNextAcquire { get; set; } = true;

        public IChunkLeaf Acquire(Transform parent, int levelOfDetail) {
            if (!ThrowsOnNextAcquire)
                return Recording.Acquire(parent, levelOfDetail);

            ThrowsOnNextAcquire = false;

            throw new InvalidOperationException(nameof(ThrowsOnNextAcquire));
        }

        public void Release(IChunkLeaf leaf) {
            Recording.Release(leaf);
        }
    }
}
