// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingChunkPresenter : IChunkPresenter {
        internal List<string> Calls { get; } = new();

        internal List<RecordingChunkLeaf> Acquired { get; } = new();

        internal List<IChunkLeaf> Released { get; } = new();

        public IChunkLeaf Acquire(Transform parent, int levelOfDetail) {
            Calls.Add(nameof(Acquire));

            var leafObject = new GameObject("Recorded Leaf");
            leafObject.transform.SetParent(parent, false);

            var leaf = new RecordingChunkLeaf(leafObject.transform, parent, levelOfDetail, Calls);
            Acquired.Add(leaf);

            return leaf;
        }

        public void Release(IChunkLeaf leaf) {
            Calls.Add(nameof(Release));
            Released.Add(leaf);
            Object.DestroyImmediate(leaf.Transform.gameObject);
        }
    }
}
