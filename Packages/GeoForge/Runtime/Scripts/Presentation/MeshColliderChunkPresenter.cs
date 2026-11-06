// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spellbound.GeoForge {
    [RequireComponent(typeof(GeoForgeManager))]
    public sealed class MeshColliderChunkPresenter : MonoBehaviour, IChunkCollisionPresenter {
        private const string ColliderName = "Chunk Collider";
        private const string PoolParentName = "ChunkColliderPool";
        private const int LeafPrefabLayer = -1;

        [Tooltip("Bakes the collision meshes of each march batch on worker threads before the colliders take them.")]
        [SerializeField]
        private bool bakeOffMainThread = true;

        [Tooltip("The layer every chunk collider is placed on. -1 takes the layer of the manager's leaf prefab.")]
        [Range(LeafPrefabLayer, 31)]
        [SerializeField]
        private int colliderLayer = LeafPrefabLayer;

        [Tooltip("The physics material every chunk collider uses. Empty takes the material of the MeshCollider " +
                 "on the manager's leaf prefab, if it has one.")]
        [SerializeField]
        private PhysicsMaterial physicsMaterial;

        private readonly Stack<MeshColliderChunkLeaf> _pool = new();

        private bool _isBeingDestroyed;
        private GeoForgeManager _manager;
        private Transform _poolParent;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _collidersHandedOut;
#endif

        public bool BakesMeshesBeforeApply => bakeOffMainThread;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterAsDefault() {
            ChunkPresenterDefaults.RegisterDefaultCollision(
                manager => manager.gameObject.AddComponent<MeshColliderChunkPresenter>());
        }

        private void Awake() {
            Initialize();
        }

        private void OnDestroy() {
            _isBeingDestroyed = true;

            while (_pool.Count > 0)
                Discard(_pool.Pop().ColliderObject);
        }

        public IChunkCollisionLeaf Acquire(Transform parent, int levelOfDetail) {
            Initialize();

            var leaf = TakeFromPool() ?? Create();
            leaf.Attach(parent);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _collidersHandedOut++;
            leaf.ColliderObject.name = $"Collider {_collidersHandedOut} at level of detail {levelOfDetail}";
#endif

            return leaf;
        }

        public void Release(IChunkCollisionLeaf leaf) {
            if (leaf is not MeshColliderChunkLeaf colliderLeaf) {
                Log.Error($"{name}: {nameof(Release)} was handed a leaf this presenter did not acquire.", this);

                return;
            }

            if (colliderLeaf.ColliderObject == null)
                return;

            if (_isBeingDestroyed) {
                Discard(colliderLeaf.ColliderObject);

                return;
            }

            colliderLeaf.Detach(_poolParent);
            _pool.Push(colliderLeaf);
        }

        private void Initialize() {
            if (_manager == null)
                _manager = GetComponent<GeoForgeManager>();

            if (_poolParent != null)
                return;

            _poolParent = new GameObject(PoolParentName).transform;
            _poolParent.SetParent(transform, false);
        }

        private MeshColliderChunkLeaf TakeFromPool() {
            var collisionScene = _manager.CollisionScene;

            while (_pool.Count > 0) {
                var leaf = _pool.Pop();

                if (leaf.ColliderObject == null)
                    continue;

                if (leaf.ColliderObject.scene == collisionScene)
                    return leaf;

                Discard(leaf.ColliderObject);
            }

            return null;
        }

        private MeshColliderChunkLeaf Create() {
            var colliderObject = new GameObject(ColliderName) { layer = ColliderLayer() };
            var collisionScene = _manager.CollisionScene;

            if (collisionScene.IsValid() && colliderObject.scene != collisionScene)
                SceneManager.MoveGameObjectToScene(colliderObject, collisionScene);

            var meshCollider = colliderObject.AddComponent<MeshCollider>();
            meshCollider.sharedMaterial = ColliderMaterial();

            return new MeshColliderChunkLeaf(colliderObject, meshCollider);
        }

        private int ColliderLayer() {
            if (colliderLayer != LeafPrefabLayer)
                return colliderLayer;

            var leafPrefab = _manager.LeafPrefab;

            return leafPrefab != null ? leafPrefab.layer : 0;
        }

        private PhysicsMaterial ColliderMaterial() {
            if (physicsMaterial != null)
                return physicsMaterial;

            var leafPrefab = _manager.LeafPrefab;

            return leafPrefab != null && leafPrefab.TryGetComponent<MeshCollider>(out var leafCollider)
                ? leafCollider.sharedMaterial
                : null;
        }

        private static void Discard(GameObject colliderObject) {
            if (colliderObject == null)
                return;

            if (Application.isPlaying)
                Destroy(colliderObject);
            else
                DestroyImmediate(colliderObject);
        }
    }
}
