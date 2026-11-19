// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    [EditorTool("Voxel Brush")]
    public sealed class VoxelBrushTool : EditorTool {
        private const float DefaultSize = 4f;
        private const float DefaultReach = 500f;
        private const float DragSpacing = 0.5f;

        [SerializeField] private VoxelStrokeShape shape;
        [SerializeField] private VoxelStrokeMode mode;
        [SerializeField] private float size = DefaultSize;
        [SerializeField] private byte material;
        [SerializeField] private float reach = DefaultReach;

        private static VoxelBrush _activatedBrush;

        private VoxelBrush _ownedBrush;
        private VoxelBrushOverlay _overlay;
        private Vector3 _lastStrokeCentre;
        private bool _isStroking;

        public VoxelBrush Brush { get; set; }

        public static VoxelBrush ActivatedBrush => _activatedBrush;

        public VoxelStrokeShape Shape {
            get => shape;
            set => shape = value;
        }

        public VoxelStrokeMode Mode {
            get => mode;
            set => mode = value;
        }

        public float Size {
            get => size;
            set => size = Mathf.Max(0f, value);
        }

        public byte Material {
            get => material;
            set => material = value;
        }

        public static void Activate(VoxelBrush brush) {
            _activatedBrush = brush ?? throw new ArgumentNullException(nameof(brush));
            ToolManager.SetActiveTool<VoxelBrushTool>();
        }

        public static void Release(VoxelBrush brush) {
            if (brush == null || _activatedBrush != brush) {
                return;
            }

            _activatedBrush = null;

            if (ToolManager.activeToolType == typeof(VoxelBrushTool)) {
                ToolManager.RestorePreviousPersistentTool();
            }
        }

        public override void OnActivated() {
            _overlay = new VoxelBrushOverlay(this);
            SceneView.AddOverlayToActiveView(_overlay);
            _overlay.displayed = true;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeOwnedBrush;
        }

        public override void OnWillBeDeactivated() {
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeOwnedBrush;

            if (_overlay != null) {
                SceneView.RemoveOverlayFromActiveView(_overlay);
                _overlay = null;
            }

            _isStroking = false;
            DisposeOwnedBrush();
        }

        private void DisposeOwnedBrush() {
            _ownedBrush?.Dispose();
            _ownedBrush = null;
        }

        public override void OnToolGUI(EditorWindow window) {
            if (window is not SceneView) {
                return;
            }

            var current = Event.current;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (current.type == EventType.Layout) {
                HandleUtility.AddDefaultControl(controlId);
                return;
            }

            if (current.type == EventType.MouseUp && _isStroking) {
                _isStroking = false;
                GUIUtility.hotControl = 0;
                current.Use();
                return;
            }

            var brush = ResolveBrush();

            if (brush == null) {
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);

            if (!brush.TryFindSolid(ray, reach, out var point)) {
                return;
            }

            switch (current.type) {
                case EventType.Repaint:
                    DrawCursor(point, ray.direction);
                    break;
                case EventType.MouseMove:
                    window.Repaint();
                    break;
                case EventType.MouseDown when current.button == 0 && !current.alt:
                    brush.Apply(StrokeAt(point));
                    _lastStrokeCentre = point;
                    _isStroking = true;
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;
                case EventType.MouseDrag when _isStroking:
                    if (Vector3.Distance(point, _lastStrokeCentre) >= size * DragSpacing) {
                        brush.Apply(StrokeAt(point));
                        _lastStrokeCentre = point;
                    }

                    current.Use();
                    break;
            }
        }

        private VoxelStroke StrokeAt(Vector3 point) {
            return new VoxelStroke(shape, mode, point, size, material);
        }

        private void DrawCursor(Vector3 point, Vector3 viewDirection) {
            if (shape == VoxelStrokeShape.Box) {
                Handles.DrawWireCube(point, Vector3.one * size);
                return;
            }

            Handles.DrawWireDisc(point, -viewDirection, size * 0.5f);
        }

        private VoxelBrush ResolveBrush() {
            if (Brush != null) {
                return Brush;
            }

            if (_activatedBrush != null) {
                return _activatedBrush;
            }

            if (_ownedBrush != null) {
                return _ownedBrush;
            }

            var volume = SceneVolumes.FirstWithEngine();

            if (volume == null) {
                return null;
            }

            _ownedBrush = new VoxelBrush(volume, RegionOf(volume.GeoVolumeEngine));

            return _ownedBrush;
        }

        private static VoxelRegion RegionOf(GeoVolumeEngine engine) {
            var minimum = Vector3Int.one * int.MaxValue;
            var maximum = Vector3Int.one * int.MinValue;

            foreach (var coordinate in engine.ChunkDict.Keys) {
                minimum = Vector3Int.Min(minimum, coordinate);
                maximum = Vector3Int.Max(maximum, coordinate);
            }

            if (engine.ChunkDict.Count == 0) {
                return VoxelRegion.Origin;
            }

            return new VoxelRegion(minimum, maximum - minimum + Vector3Int.one);
        }
    }
}
