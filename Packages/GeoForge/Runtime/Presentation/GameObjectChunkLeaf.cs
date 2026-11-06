// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Presentation {
    internal sealed class GameObjectChunkLeaf : IChunkLeaf {
        private static readonly int WorldToLocalProperty = Shader.PropertyToID("_WorldToLocal");

        private readonly MeshFilter _leafFilter;
        private readonly LeafBehaviour _leafBehaviour;
        private readonly MeshRenderer _leafRenderer;
        private readonly MaterialPropertyBlock _propertyBlock;
        private readonly MeshFilter _transitionFilter;
        private readonly MeshRenderer _transitionRenderer;

        internal GameObjectChunkLeaf(
            GameObject leafObject, GameObject transitionObject, MaterialPropertyBlock propertyBlock) {
            LeafObject = leafObject;
            TransitionObject = transitionObject;
            _propertyBlock = propertyBlock;

            leafObject.TryGetComponent(out _leafFilter);
            leafObject.TryGetComponent(out _leafRenderer);
            leafObject.TryGetComponent(out _leafBehaviour);
            transitionObject.TryGetComponent(out _transitionFilter);
            transitionObject.TryGetComponent(out _transitionRenderer);
        }

        internal GameObject LeafObject { get; }

        internal GameObject TransitionObject { get; }

        public void SetMesh(Mesh mesh, int levelOfDetail) {
            if (_leafFilter != null)
                _leafFilter.sharedMesh = mesh;

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

        internal void ClearMeshes() {
            if (_transitionFilter != null)
                _transitionFilter.sharedMesh = null;

            if (_leafFilter != null)
                _leafFilter.sharedMesh = null;
        }
    }
}
