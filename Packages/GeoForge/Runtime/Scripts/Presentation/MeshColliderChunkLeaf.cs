// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core.Logging;
using UnityEngine;

namespace Spellbound.GeoForge {
    internal sealed class MeshColliderChunkLeaf : IChunkCollisionLeaf {
        private readonly MeshCollider _meshCollider;

        private Transform _followedParent;
        private bool _knowsParentInVolume;
        private Matrix4x4 _parentInVolume;
        private bool _refusedDestroyedCollider;

        internal MeshColliderChunkLeaf(GameObject colliderObject, MeshCollider meshCollider) {
            ColliderObject = colliderObject;
            _meshCollider = meshCollider;
        }

        internal GameObject ColliderObject { get; }

        public bool HasMesh => _meshCollider != null && _meshCollider.sharedMesh != null;

        public void SetMesh(Mesh mesh) {
            if (_meshCollider == null) {
                RefuseDestroyedCollider();

                return;
            }

            _meshCollider.sharedMesh = mesh;
        }

        public void SetVolumeMatrix(Matrix4x4 worldToLocal) {
            if (_followedParent == null || ColliderObject == null)
                return;

            if (!_knowsParentInVolume) {
                _parentInVolume = worldToLocal * _followedParent.localToWorldMatrix;
                _knowsParentInVolume = true;
            }

            var colliderToWorld = worldToLocal.inverse * _parentInVolume;
            var colliderTransform = ColliderObject.transform;
            colliderTransform.SetPositionAndRotation(colliderToWorld.GetPosition(), colliderToWorld.rotation);
            colliderTransform.localScale = colliderToWorld.lossyScale;
        }

        internal void Attach(Transform parent) {
            var colliderTransform = ColliderObject.transform;
            _knowsParentInVolume = false;

            if (parent.gameObject.scene == ColliderObject.scene) {
                _followedParent = null;
                colliderTransform.SetParent(parent, false);
                colliderTransform.localPosition = Vector3.zero;
                colliderTransform.localRotation = Quaternion.identity;
                colliderTransform.localScale = Vector3.one;
            }
            else {
                _followedParent = parent;
                colliderTransform.SetParent(null, false);
                colliderTransform.SetPositionAndRotation(parent.position, parent.rotation);
                colliderTransform.localScale = parent.lossyScale;
            }

            ColliderObject.SetActive(true);
        }

        internal void Detach(Transform poolParent) {
            if (_meshCollider != null)
                _meshCollider.sharedMesh = null;

            _followedParent = null;
            _knowsParentInVolume = false;
            ColliderObject.SetActive(false);

            var samePoolScene = poolParent != null && poolParent.gameObject.scene == ColliderObject.scene;
            ColliderObject.transform.SetParent(samePoolScene ? poolParent : null, false);
        }

        private void RefuseDestroyedCollider() {
            if (_refusedDestroyedCollider)
                return;

            _refusedDestroyedCollider = true;
            Log.Error("A chunk collider was destroyed while its chunk still used it, so that chunk stops " +
                      "colliding until its node is rebuilt.");
        }
    }
}
