// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Presentation {
    internal sealed class GameObjectChunkLeaf : IChunkLeaf {
        private static readonly int WorldToLocalProperty = Shader.PropertyToID("_WorldToLocal");

        private readonly MeshCollider _leafCollider;
        private readonly MeshFilter _leafFilter;
        private readonly LeafBehaviour _leafBehaviour;
        private readonly MeshRenderer _leafRenderer;
        private readonly MaterialPropertyBlock _propertyBlock;
        private readonly MeshCollider _transitionCollider;
        private readonly MeshFilter _transitionFilter;
        private readonly MeshRenderer _transitionRenderer;

        internal GameObjectChunkLeaf(
            GameObject leafObject, GameObject transitionObject, MaterialPropertyBlock propertyBlock) {
            LeafObject = leafObject;
            TransitionObject = transitionObject;
            _propertyBlock = propertyBlock;

            leafObject.TryGetComponent(out _leafFilter);
            leafObject.TryGetComponent(out _leafCollider);
            leafObject.TryGetComponent(out _leafRenderer);
            leafObject.TryGetComponent(out _leafBehaviour);
            transitionObject.TryGetComponent(out _transitionFilter);
            transitionObject.TryGetComponent(out _transitionCollider);
            transitionObject.TryGetComponent(out _transitionRenderer);
        }

        internal GameObject LeafObject { get; }

        internal GameObject TransitionObject { get; }

        public Transform Transform => LeafObject.transform;

        public string Name {
            get => LeafObject.name;
            set {
                LeafObject.name = value;
                TransitionObject.name = $"{value} Transition";
            }
        }

        public void SetMesh(Mesh mesh, int levelOfDetail) {
            if (_leafFilter != null)
                _leafFilter.sharedMesh = mesh;

            if (_leafCollider != null)
                _leafCollider.sharedMesh = mesh;

            if (_leafBehaviour != null)
                _leafBehaviour.OnMeshUpdated(levelOfDetail);
        }

        public void SetTransitionMesh(Mesh mesh) {
            if (_transitionFilter != null)
                _transitionFilter.sharedMesh = mesh;
        }

        public void SetVolumeMatrix(Matrix4x4 worldToLocal) {
            _propertyBlock.Clear();
            _propertyBlock.SetMatrix(WorldToLocalProperty, worldToLocal);

            if (_leafRenderer != null)
                _leafRenderer.SetPropertyBlock(_propertyBlock);

            if (_transitionRenderer != null)
                _transitionRenderer.SetPropertyBlock(_propertyBlock);
        }

        public void SetVisible(bool visible) {
            LeafObject.SetActive(visible);
        }

        internal void ClearMeshes() {
            if (_transitionCollider != null)
                _transitionCollider.sharedMesh = null;

            if (_transitionFilter != null)
                _transitionFilter.sharedMesh = null;

            if (_leafCollider != null)
                _leafCollider.sharedMesh = null;

            if (_leafFilter != null)
                _leafFilter.sharedMesh = null;
        }
    }
}
