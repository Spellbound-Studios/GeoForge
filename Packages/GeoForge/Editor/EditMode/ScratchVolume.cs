// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Logging;
using Spellbound.GeoForge.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.EditMode {
    public sealed class ScratchVolume : IDisposable {
        private const string SceneName = "GeoForge Scratch Volume";
        private const string RenderingProfilePath =
            "Packages/com.spellboundstudios.geoforge/Runtime/Defaults/JobAndRenderProfiles/BarycentricProfile.asset";
        private const int DefaultCompletionTickLimit = 2000;

        private readonly List<Object> _ownedObjects = new();
        private readonly bool _isPreview;
        private GeoVolume _volume;
        private bool _isDisposed;

        private ScratchVolume(Scene scene, bool isPreview) {
            Scene = scene;
            _isPreview = isPreview;
        }

        public IGeoVolume Volume => _volume;

        public Scene Scene { get; }

        public GeoForgeManager Manager { get; private set; }

        public int CompletionTickLimit { get; set; } = DefaultCompletionTickLimit;

        public static ScratchVolume Create(int chunkSize, float voxelSize, Vector3Int chunkExtent, bool visible) {
            var config = VoxelVolumeConfig.Create(chunkSize, voxelSize, chunkExtent);
            config.hideFlags = HideFlags.HideAndDontSave;

            return Open(config, visible);
        }

        public static ScratchVolume Create(VoxelVolumeConfig config, Vector3Int chunkExtent, bool visible) {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var sized = VoxelVolumeConfig.Create(config, chunkExtent);
            sized.hideFlags = HideFlags.HideAndDontSave;

            return Open(sized, visible);
        }

        public void Complete() {
            for (var tick = 0; tick < CompletionTickLimit; tick++) {
                Manager.Tick();

                if (!Manager.HasPendingWork)
                    return;
            }

            Log.Emit(LogLevel.Warning,
                $"{SceneName}: work is still pending after {CompletionTickLimit} ticks.", Manager);
        }

        public void Dispose() {
            if (_isDisposed)
                return;

            _isDisposed = true;
            EditorApplication.update -= Tick;

            if (_volume != null)
                _volume.GeoVolumeEngine?.Dispose();

            if (Manager != null)
                Manager.ReleaseNativeResources();

            if (Scene.IsValid()) {
                if (_isPreview)
                    EditorSceneManager.ClosePreviewScene(Scene);
                else
                    EditorSceneManager.CloseScene(Scene, true);
            }

            foreach (var owned in _ownedObjects) {
                if (owned != null)
                    Object.DestroyImmediate(owned);
            }

            _ownedObjects.Clear();
        }

        private static ScratchVolume Open(VoxelVolumeConfig config, bool visible) {
            var scene = visible
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)
                : EditorSceneManager.NewPreviewScene();
            scene.name = SceneName;

            var scratch = new ScratchVolume(scene, !visible);
            scratch._ownedObjects.Add(config);

            try {
                scratch.Build(config);
            }
            catch {
                scratch.Dispose();

                throw;
            }

            return scratch;
        }

        private void Build(VoxelVolumeConfig config) {
            var managerObject = CreateInScene("GeoForge Manager");
            Manager = managerObject.AddComponent<GeoForgeManager>();
            Manager.jobAndRenderProfile = LoadRenderingProfile();
            Manager.LeafPrefab = CreateTemplate("Leaf Template", managerObject.transform, typeof(MeshFilter),
                typeof(MeshRenderer));
            Manager.BuildMarchTables();
            Manager.Presenter = managerObject.AddComponent<GameObjectChunkPresenter>();
            Manager.CollisionPresenter = managerObject.AddComponent<MeshColliderChunkPresenter>();

            _volume = CreateInScene("GeoForge Volume").AddComponent<GeoVolume>();
            _volume.Manager = Manager;
            _volume.Config = config;
            _volume.ChunkPrefab = CreateTemplate("Chunk Template", managerObject.transform, typeof(GeoChunk));
            _volume.IsPrimaryTerrain = true;

            if (!_volume.Build())
                throw new InvalidOperationException($"{SceneName}: the volume could not be built.");

            PlaceViewer();
            EditorApplication.update += Tick;
        }

        private void PlaceViewer() {
            var engine = _volume.GeoVolumeEngine;
            var bounds = new Bounds(engine.Transform.position, Vector3.zero);

            foreach (var coordinate in engine.ChunkDict.Keys) {
                bounds.Encapsulate(engine.ChunkWorldOrigin(coordinate));
                bounds.Encapsulate(engine.ChunkWorldOrigin(coordinate + Vector3Int.one));
            }

            var radius = bounds.size.magnitude;
            engine.SetViewers(new[] { new ChunkViewer(engine.Transform.position, radius, radius) });
        }

        private void Tick() {
            if (Manager != null)
                Manager.Tick();
        }

        private GameObject CreateInScene(string objectName) {
            var created = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(created, Scene);

            return created;
        }

        private static GameObject CreateTemplate(string objectName, Transform parent, params Type[] components) {
            var template = new GameObject(objectName, components);
            template.transform.SetParent(parent, false);

            return template;
        }

        private MaterialRenderingProfile LoadRenderingProfile() {
            var profile = AssetDatabase.LoadAssetAtPath<MaterialRenderingProfile>(RenderingProfilePath);

            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            _ownedObjects.Add(profile);

            return profile;
        }
    }
}
