// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.Logging;
using UnityEngine;

namespace Spellbound.GeoForge.Presentation {
    [RequireComponent(typeof(GeoForgeManager))]
    public sealed class GameObjectChunkPresenter : MonoBehaviour, IChunkPresenter {
        private const string PoolParentName = "OctreeLeafPool";

        private readonly Stack<GameObject> _pool = new();

        private bool _isBeingDestroyed;
        private GeoForgeManager _manager;
        private Transform _poolParent;
        private MaterialPropertyBlock _propertyBlock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterAsDefault() {
            ChunkPresenterDefaults.Factory = manager => manager.gameObject.AddComponent<GameObjectChunkPresenter>();
        }

        private void Awake() {
            Initialize();
        }

        private void OnDestroy() {
            _isBeingDestroyed = true;

            while (_pool.Count > 0)
                Discard(_pool.Pop());
        }

        public IChunkLeaf Acquire(Transform parent, int levelOfDetail) {
            Initialize();

            var leafObject = Take(parent);
            var transitionObject = Take(leafObject.transform);

            return new GameObjectChunkLeaf(leafObject, transitionObject, _propertyBlock);
        }

        public void Release(IChunkLeaf leaf) {
            if (leaf is not GameObjectChunkLeaf gameObjectLeaf) {
                Log.Error($"{name}: {nameof(Release)} was handed a leaf this presenter did not acquire.", this);

                return;
            }

            gameObjectLeaf.ClearMeshes();
            Return(gameObjectLeaf.TransitionObject);
            Return(gameObjectLeaf.LeafObject);
        }

        private void Initialize() {
            if (_manager == null)
                _manager = GetComponent<GeoForgeManager>();

            _propertyBlock ??= new MaterialPropertyBlock();

            if (_poolParent != null)
                return;

            _poolParent = new GameObject(PoolParentName).transform;
            _poolParent.SetParent(transform, false);
        }

        private GameObject Take(Transform parent) {
            var pooledObject = TakeFromPool();

            if (pooledObject == null)
                pooledObject = Create();

            var pooledTransform = pooledObject.transform;

            pooledTransform.SetParent(parent, false);
            pooledTransform.localPosition = Vector3.zero;
            pooledTransform.localRotation = Quaternion.identity;

            return pooledObject;
        }

        private GameObject TakeFromPool() {
            while (_pool.Count > 0) {
                var pooledObject = _pool.Pop();

                if (pooledObject == null)
                    continue;

                pooledObject.SetActive(true);

                return pooledObject;
            }

            return null;
        }

        private GameObject Create() {
            var createdObject = Instantiate(_manager.LeafPrefab);
            var profile = _manager.jobAndRenderProfile;

            if (profile != null && createdObject.TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.sharedMaterial = profile.Material;

            return createdObject;
        }

        private void Return(GameObject pooledObject) {
            if (pooledObject == null)
                return;

            if (_isBeingDestroyed) {
                Discard(pooledObject);

                return;
            }

            pooledObject.SetActive(false);
            pooledObject.transform.SetParent(_poolParent, false);
            _pool.Push(pooledObject);
        }

        private static void Discard(GameObject pooledObject) {
            if (pooledObject == null)
                return;

            if (Application.isPlaying)
                Destroy(pooledObject);
            else
                DestroyImmediate(pooledObject);
        }
    }
}
