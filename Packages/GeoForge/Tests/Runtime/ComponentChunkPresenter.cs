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

            return new Leaf(leafObject.transform);
        }

        public void Release(IChunkLeaf leaf) {
            Released++;
            Object.DestroyImmediate(leaf.Transform.gameObject);
        }

        private sealed class Leaf : IChunkLeaf {
            public Leaf(Transform transform) {
                Transform = transform;
            }

            public Transform Transform { get; }

            public string Name {
                get => Transform.name;
                set => Transform.name = value;
            }

            public void SetMesh(Mesh mesh, int levelOfDetail) { }

            public void SetTransitionMesh(Mesh mesh) { }

            public void SetVolumeMatrix(Matrix4x4 worldToLocal) { }

            public void SetVisible(bool visible) {
                Transform.gameObject.SetActive(visible);
            }
        }
    }
}
