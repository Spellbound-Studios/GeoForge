// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections;
using Spellbound.Core.Logging;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Basic Implementation of IGeoVolume for a Volume of Finite Size.
    ///     Initializes Chunks one per frame until all are initialized.
    ///     All other management is the baseline wrappers for GeoVolume.
    ///     Note IGeoVolume implementations are NOT virtual. The intent of GeoVolume is to be extendable,
    ///     but not in terms of altering it how it implements IGeoVolume. If you want a unique implementation of
    ///     IGeoVolume, create a new class instead of inheriting from GeoVolume.
    /// </summary>
    public class GeoVolume : MonoBehaviour, IGeoVolume {
        [Tooltip("The manager this volume belongs to. If left unset, the manager on a parent is used.")]
        [SerializeField]
        private GeoForgeManager manager;

        [field: Tooltip("Preset for what voxel data is generated in the geoVolume")]
        [field: SerializeField]
        protected GeoForgeDataGenerator DataGenerator { get; set; }

        [field: Tooltip("Rules for immutable voxels on the external faces of the geoVolume")]
        [field: SerializeField]
        protected BoundaryOverrides BoundaryOverrides { get; set; }

        [field: Header("Volume Settings")]
        [field: Tooltip("Config for ChunkSize, VolumeSize, etc")]
        [field: SerializeField]
        protected VoxelVolumeConfig Config { get; set; }

        [field: Tooltip("Prefab for the Chunk the Volume will build itself from. Must Implement IGeoChunk")]
        [field: SerializeField]
        private GameObject ChunkPrefab { get; set; }

        private IDensitySource _pendingDensitySource;
        private IGeoEditStoreSource _editStores = new GeoEditStoreSource();

        /// <summary>
        ///     Chunk Prefab must have a IGeoChunk component.
        ///     All IVolumes should create VoxelCoreLogic on Awake.
        /// </summary>
        protected virtual void Awake() {
            if (ChunkPrefab == null || !ChunkPrefab.TryGetComponent<IGeoChunk>(out _)) {
                Debug.LogError($"{name}: _chunkPrefab is null or does not have IGeoChunk Component");
                enabled = false;

                return;
            }

            CreateGeoVolume(Config);
        }

        /// <summary>
        ///     Marching Cubes meshes utilize a triplanar shader. In order for textures to "stick to" their gemometry
        ///     as the geoVolume moves, the geoVolume origin must be updated. This is costly so should be avoided for
        ///     volumes that reliably will not move.
        /// </summary>
        protected virtual void Update() {
            if (!IsMoving)
                return;

            GeoVolumeEngine.UpdateVolumeOrigin();
        }

        /// <summary>
        ///     This must be done on ALL IGeoVolume implementers to prevent memory leaks.
        /// </summary>
        protected virtual void OnDestroy() {
            GeoVolumeEngine?.Dispose();
        }

#if UNITY_EDITOR
        /// <summary>
        ///     Enforces a floor on view distances to prohibit abrupt changes.
        ///     The TransVoxel Algorithm does not handle abrupt changes so they would leave visible seams.
        /// </summary>
        protected virtual void OnValidate() {
            if (Config == null) {
                ViewDistanceLodRanges = null;

                return;
            }

            ViewDistanceLodRanges = GeoVolumeEngine.ValidateLodRanges(ViewDistanceLodRanges, Config);
        }
#endif

        [field: Tooltip("Initial State for if the geoVolume is moving. " +
                        "If true it updates the origin of the triplanar material shader")]
        [field: SerializeField]
        public bool IsMoving { get; set; }

        [field: Tooltip("Initial State for if the geoVolume is the Primary Terrain. " +
                        "Affects whether it can be globally queried or not")]
        [field: SerializeField]
        public bool IsPrimaryTerrain { get; set; }

        [field: Tooltip("View Distances to each Level of Detail. Enforces a floor to prohibit abrupt changes")]
        [field: SerializeField]
        public Vector2[] ViewDistanceLodRanges { get; protected set; }

        public GeoVolumeEngine GeoVolumeEngine { get; private set; }

        public IDensitySource DensitySource =>
            GeoVolumeEngine != null ? GeoVolumeEngine.DensitySource : _pendingDensitySource;

        public void UseDensitySource(IDensitySource source) {
            if (GeoVolumeEngine == null) {
                _pendingDensitySource = source;

                return;
            }

            GeoVolumeEngine.UseDensitySource(source);
        }

        public void UseEditStores(IGeoEditStoreSource source) {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (GeoVolumeEngine != null && GeoVolumeEngine.ChunkDict.Count > 0) {
                Log.Error($"{name}: edit stores are given before the first chunk is made; " +
                          $"{GeoVolumeEngine.ChunkDict.Count} chunks already hold theirs, so the source is refused.",
                    this);

                return;
            }

            _editStores = source;
        }

        public IGeoEditStoreSource EditStores => _editStores;

        public void ForgetEditStores(IGeoEditStoreSource source) {
            if (source != null && ReferenceEquals(_editStores, source))
                _editStores = new GeoEditStoreSource();
        }

        public void ResetEditedChunksToProcedural() {
            if (GeoVolumeEngine != null && !GeoVolumeEngine.TryResetEditedChunksToProcedural(DataGenerator))
                return;

            _editStores.ClearStores();
        }

        void IGeoVolume.HandleAllChunksMeshed() {
            OnAllChunksMeshed();
        }

        public Transform VolumeTransform => transform;

        protected virtual void CreateGeoVolume(VoxelVolumeConfig voxelVolumeConfig) {
            var volumeManager = manager != null ? manager : GetComponentInParent<GeoForgeManager>();

            if (volumeManager == null) {
                Log.Error($"{name}: no {nameof(GeoForgeManager)} is assigned or on a parent, so nothing is built.",
                    this);
                enabled = false;

                return;
            }

            GeoVolumeEngine = new GeoVolumeEngine(this, this, voxelVolumeConfig, volumeManager);

            if (_pendingDensitySource != null) {
                GeoVolumeEngine.UseDensitySource(_pendingDensitySource);
                _pendingDensitySource = null;
            }

            GeoVolumeEngine.RegisterVolume();
            OnVolumeRegistered();
            StartCoroutine(InitializeChunks());
        }

        protected virtual void OnAllChunksMeshed() { }


        /// <summary>
        ///     Initializes Chunks one per frame, centered on the Volume's transform
        ///     One NativeArray of Voxels is maintained for all the chunks and simply overriden with new data.
        /// </summary>
        protected virtual IEnumerator InitializeChunks() {
            var size = GeoVolumeEngine.ConfigBlob.Value.SizeInChunks;
            var offset = new Vector3Int(size.x / 2, size.y / 2, size.z / 2);

            for (var x = 0; x < size.x; x++)
            for (var y = 0; y < size.y; y++)
            for (var z = 0; z < size.z; z++) {
                var chunkCoord = new Vector3Int(x, y, z) - offset;
                var chunk = GeoVolumeEngine.CreateChunk<IGeoChunk, IGeoEditStore>(chunkCoord, ChunkPrefab,
                    _editStores.CreateFor(chunkCoord));

                if (!GeoVolumeEngine.RegisterChunk(chunkCoord, chunk)) {
                    Debug.LogError($"{name}: failed to register chunk at {chunkCoord} - duplicate coord?");
                    Destroy(chunk.GeoChunkEngine.Transform.gameObject);

                    yield return null;

                    continue;
                }

                chunk.SetBoundaryOverrides(chunkCoord, GeoVolumeEngine.ConfigBlob, BoundaryOverrides);
                chunk.SetVoxels(GeoVolumeEngine.CreateFillRequest(chunkCoord),
                    GeoVolumeEngine.DensitySourceFor(DataGenerator));

                yield return null;
            }

            OnChunksInitialized();
        }

        /// <summary>
        ///     Called once GeoVolumeEngine has been created and registered with GeoForgeManager, but
        ///     before any chunks exist yet - the earliest point external systems can safely read
        ///     GeoVolumeEngine/ConfigBlob/bounds off this volume.
        /// </summary>
        protected virtual void OnVolumeRegistered() { }

        /// <summary>
        ///     Called once every chunk in the volume has been created, registered, had its boundary
        ///     overrides applied, and had its initial voxel data set - i.e. once
        ///     GeoVolumeEngine.AllChunksReady() is guaranteed true. Safe point to call
        ///     TryLoadFromByteArray.
        /// </summary>
        protected virtual void OnChunksInitialized() { }
    }
}
