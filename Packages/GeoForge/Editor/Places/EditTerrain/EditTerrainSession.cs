// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Authoring;
using Spellbound.GeoForge.EditMode;
using Spellbound.Places;
using Spellbound.Places.Owners;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places {
    public sealed class EditTerrainSession {
        public const float DefaultVoxelSize = 1f;

        private const string PreviewRootName = "Place Preview";

        private readonly List<string> _sealedPlacements = new();
        private ScratchVolume _scratch;

        private EditTerrainSession(PlaceDefinition place, int layerIndex, VoxelLayer layer, float voxelSize) {
            Place = place;
            LayerIndex = layerIndex;
            Layer = layer;
            VoxelSize = voxelSize;
            Region = new VoxelRegion(Vector3Int.zero, layer.ChunkExtent);
            StampReport = new ValidationReport(place.name);
        }

        public PlaceDefinition Place { get; }

        public int LayerIndex { get; }

        public VoxelLayer Layer { get; }

        public float VoxelSize { get; }

        public VoxelRegion Region { get; }

        public ValidationReport StampReport { get; }

        public VoxelBrush Brush { get; private set; }

        public GameObject PreviewRoot { get; private set; }

        public IGeoVolume Volume => _scratch?.Volume;

        public Scene Scene => _scratch?.Scene ?? default;

        public bool IsOpen => _scratch != null;

        public IReadOnlyList<string> SealedPlacements => _sealedPlacements;

        public static EditTerrainSession Open(PlaceDefinition place, int layerIndex) {
            return Open(place, layerIndex, DefaultVoxelSize);
        }

        public static EditTerrainSession Open(PlaceDefinition place, int layerIndex, float voxelSize) {
            if (place == null) {
                throw new ArgumentNullException(nameof(place));
            }

            if (layerIndex < 0 || layerIndex >= place.Layers.Count) {
                throw new ArgumentOutOfRangeException(nameof(layerIndex), layerIndex,
                    "The place '" + place.name + "' has " + place.Layers.Count + " layer slots.");
            }

            if (place.Layers[layerIndex] is not VoxelLayer layer) {
                throw new ArgumentException("Layer slot " + layerIndex + " of the place '" + place.name +
                                            "' is not a voxel layer.", nameof(layerIndex));
            }

            if (voxelSize <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(voxelSize), voxelSize, "A voxel has a positive size.");
            }

            var session = new EditTerrainSession(place, layerIndex, layer, voxelSize);

            try {
                session.Build();
            }
            catch {
                session.Dispose();

                throw;
            }

            return session;
        }

        public bool TryClose(ValidationReport report) {
            if (report == null) {
                throw new ArgumentNullException(nameof(report));
            }

            if (!IsOpen) {
                throw new InvalidOperationException("The edit terrain session of '" + Place.name + "' is closed.");
            }

            _sealedPlacements.Clear();
            var captured = GeoForgePlacesKit.Capture(_scratch.Volume, Region, Layer.LayerName, report);

            if (captured == null || report.HasErrors) {
                return false;
            }

            CheckPlacements(captured, report);

            if (report.HasErrors) {
                return false;
            }

            Undo.RecordObject(Place, "Edit terrain");

            if (Layer.Store != null) {
                Undo.RecordObject(Layer.Store, "Edit terrain");
            }

            VoxelRunStores.Write(Place, Layer, captured);
            EditorUtility.SetDirty(Place);
            AssetDatabase.SaveAssets();
            Dispose();

            return true;
        }

        public void Cancel() {
            Dispose();
        }

        private void Build() {
            RefuseBesideAnUntitledScene();
            _scratch = ScratchVolume.Create(Layer.ChunkSize, VoxelSize, Layer.ChunkExtent, true);
            PlaceFirstVoxelAtThePlaceOrigin();
            new VoxelLayerOwner(_scratch.Volume, Region).Stamp(Layer, new PlaceStampContext(StampReport));

            if (StampReport.HasErrors) {
                throw new InvalidOperationException("The voxel layer '" + Layer.LayerName + "' of '" + Place.name +
                                                    "' could not be stamped: " + StampReport.ToText());
            }

            _scratch.Complete();
            StampOtherLayers();
            Brush = new VoxelBrush(_scratch.Volume, Region);
            VoxelBrushTool.Activate(Brush);
        }

        private void PlaceFirstVoxelAtThePlaceOrigin() {
            var engine = _scratch.Volume.GeoVolumeEngine;
            ref var config = ref engine.ConfigBlob.Value;
            engine.Transform.position = -(Vector3)config.Offset * config.Resolution;
        }

        private Vector3 FirstVoxelPosition() {
            var engine = _scratch.Volume.GeoVolumeEngine;
            ref var config = ref engine.ConfigBlob.Value;

            return engine.Transform.TransformPoint((Vector3)config.Offset * config.Resolution);
        }

        private void RefuseBesideAnUntitledScene() {
            for (var index = 0; index < SceneManager.sceneCount; index++) {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path)) {
                    throw new InvalidOperationException("Edit terrain of '" + Place.name +
                                                        "' opens its scratch scene beside the open scenes; " +
                                                        "save the untitled scene first.");
                }
            }
        }

        private void StampOtherLayers() {
            PreviewRoot = new GameObject(PreviewRootName);
            SceneManager.MoveGameObjectToScene(PreviewRoot, _scratch.Scene);
            var rootsBefore = new HashSet<GameObject>(_scratch.Scene.GetRootGameObjects());
            var activeScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(_scratch.Scene);

            try {
                var context = new PlaceStampContext(StampReport, preview: true);

                for (var index = 0; index < Place.Layers.Count; index++) {
                    var other = Place.Layers[index];

                    if (index == LayerIndex || other == null) {
                        continue;
                    }

                    PlaceLayerOwnerRegistry.Default.TryStamp(other, context);
                }
            }
            finally {
                if (activeScene.IsValid() && activeScene.isLoaded) {
                    SceneManager.SetActiveScene(activeScene);
                }
            }

            foreach (var root in _scratch.Scene.GetRootGameObjects()) {
                if (!rootsBefore.Contains(root)) {
                    root.transform.SetParent(PreviewRoot.transform, true);
                }
            }

            foreach (var previewed in PreviewRoot.GetComponentsInChildren<Transform>(true)) {
                previewed.gameObject.hideFlags |= HideFlags.NotEditable;
            }
        }

        private void CheckPlacements(VoxelLayer captured, ValidationReport report) {
            var transient = ScriptableObject.CreateInstance<PlaceDefinition>();
            transient.hideFlags = HideFlags.HideAndDontSave;

            try {
                for (var index = 0; index < Place.Layers.Count; index++) {
                    var layer = index == LayerIndex ? captured : Place.Layers[index];

                    if (layer != null) {
                        transient.AddLayer(layer);
                    }
                }

                var origin = FirstVoxelPosition();
                var firstFinding = report.Findings.Count;
                new PlacementsInRockCheck(PlacementsInRockCheck.DefaultPlacementBoxSize, origin, VoxelSize)
                    .Check(transient, report);

                for (var index = firstFinding; index < report.Findings.Count; index++) {
                    var finding = report.Findings[index];

                    if (finding.Code == GeoForgePlacesFindingCodes.InsideRock) {
                        _sealedPlacements.Add(finding.Location);
                    }
                }
            }
            finally {
                Object.DestroyImmediate(transient);
            }
        }

        private void Dispose() {
            if (Brush != null) {
                VoxelBrushTool.Release(Brush);
                Brush.Dispose();
                Brush = null;
            }

            if (_scratch != null) {
                _scratch.Dispose();
                _scratch = null;
            }

            PreviewRoot = null;
        }
    }
}
