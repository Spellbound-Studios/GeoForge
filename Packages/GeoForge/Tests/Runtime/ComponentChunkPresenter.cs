// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public sealed class ComponentChunkPresenter : MonoBehaviour, IChunkPresenter {
        public int Acquired { get; private set; }

        public int Released { get; private set; }

        public IChunkLeaf Acquire(Transform parent, int levelOfDetail) {
            Acquired++;
            var leafObject = new GameObject("Component Leaf");
            leafObject.transform.SetParent(parent, false);

            return new Leaf(leafObject);
        }

        public void Release(IChunkLeaf leaf) {
            Released++;
            Object.DestroyImmediate(((Leaf)leaf).LeafObject);
        }

        private sealed class Leaf : IChunkLeaf {
            public Leaf(GameObject leafObject) {
                LeafObject = leafObject;
            }

            public GameObject LeafObject { get; }

            public void SetMesh(Mesh mesh, int levelOfDetail) { }

            public void SetTransitionMesh(Mesh mesh) { }

            public void SetVolumeMatrix(Matrix4x4 worldToLocal) { }
        }
    }
}
