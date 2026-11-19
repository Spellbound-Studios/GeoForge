// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Spellbound.GeoForge.Places {
    public sealed class VoxelBrushOverlay : Overlay, ICreateHorizontalToolbar, ICreateVerticalToolbar {
        private const string OverlayName = "Voxel Brush";
        private const float SizeFieldWidth = 120f;

        private readonly VoxelBrushTool _tool;

        public VoxelBrushOverlay(VoxelBrushTool tool) {
            _tool = tool;
            displayName = OverlayName;
        }

        public override VisualElement CreatePanelContent() {
            var panel = new VisualElement();
            AddFields(panel);

            return panel;
        }

        public OverlayToolbar CreateHorizontalToolbarContent() {
            var toolbar = new OverlayToolbar();
            AddFields(toolbar);

            return toolbar;
        }

        public OverlayToolbar CreateVerticalToolbarContent() {
            return CreateHorizontalToolbarContent();
        }

        private void AddFields(VisualElement container) {
            var shapeField = new EnumField("Shape", _tool.Shape);
            shapeField.RegisterValueChangedCallback(change => _tool.Shape = (VoxelStrokeShape)change.newValue);
            container.Add(shapeField);

            var modeField = new EnumField("Mode", _tool.Mode);
            modeField.RegisterValueChangedCallback(change => _tool.Mode = (VoxelStrokeMode)change.newValue);
            container.Add(modeField);

            var sizeField = new FloatField("Size (m)") { value = _tool.Size };
            sizeField.style.minWidth = SizeFieldWidth;
            sizeField.RegisterValueChangedCallback(change => _tool.Size = change.newValue);
            container.Add(sizeField);

            var materialField = new SliderInt("Material", byte.MinValue, byte.MaxValue) {
                value = _tool.Material,
                showInputField = true
            };
            materialField.RegisterValueChangedCallback(change =>
                _tool.Material = (byte)Mathf.Clamp(change.newValue, byte.MinValue, byte.MaxValue));
            container.Add(materialField);
        }
    }
}
