// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingCollisionPresenter : IChunkCollisionPresenter {
        internal List<string> Calls { get; } = new();

        internal List<RecordingCollisionLeaf> Acquired { get; } = new();

        internal List<IChunkCollisionLeaf> Released { get; } = new();

        public bool BakesMeshesBeforeApply { get; set; }

        internal bool HandsOutThrowingLeaves { get; set; }

        public IChunkCollisionLeaf Acquire(Transform parent, int levelOfDetail) {
            Calls.Add(nameof(Acquire));

            var leafObject = new GameObject("Recorded Collision Leaf");
            leafObject.transform.SetParent(parent, false);

            var leaf = new RecordingCollisionLeaf(leafObject, parent, levelOfDetail, Calls) {
                ThrowsOnSetMesh = HandsOutThrowingLeaves
            };
            Acquired.Add(leaf);

            return leaf;
        }

        public void Release(IChunkCollisionLeaf leaf) {
            Calls.Add(nameof(Release));
            Released.Add(leaf);
            Object.DestroyImmediate(((RecordingCollisionLeaf)leaf).LeafObject);
        }
    }
}
