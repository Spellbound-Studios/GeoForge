// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingChunkLeaf : IChunkLeaf {
        private readonly List<string> _calls;

        internal RecordingChunkLeaf(Transform transform, Transform parent, int acquiredLevelOfDetail,
            List<string> calls) {
            Transform = transform;
            Parent = parent;
            AcquiredLevelOfDetail = acquiredLevelOfDetail;
            _calls = calls;
        }

        internal Transform Parent { get; }

        internal int AcquiredLevelOfDetail { get; }

        internal Mesh Mesh { get; private set; }

        internal int MeshLevelOfDetail { get; private set; }

        internal Mesh TransitionMesh { get; private set; }

        internal Matrix4x4 VolumeMatrix { get; private set; }

        internal bool IsVisible { get; private set; }

        public Transform Transform { get; }

        public string Name {
            get => Transform.name;
            set => Transform.name = value;
        }

        public void SetMesh(Mesh mesh, int levelOfDetail) {
            _calls.Add(nameof(SetMesh));
            Mesh = mesh;
            MeshLevelOfDetail = levelOfDetail;
        }

        public void SetTransitionMesh(Mesh mesh) {
            _calls.Add(nameof(SetTransitionMesh));
            TransitionMesh = mesh;
        }

        public void SetVolumeMatrix(Matrix4x4 worldToLocal) {
            _calls.Add(nameof(SetVolumeMatrix));
            VolumeMatrix = worldToLocal;
        }

        public void SetVisible(bool visible) {
            _calls.Add(nameof(SetVisible));
            IsVisible = visible;
        }
    }
}
