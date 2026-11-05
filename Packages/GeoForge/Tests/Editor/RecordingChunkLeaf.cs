// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingChunkLeaf : IChunkLeaf {
        private readonly List<string> _calls;

        internal RecordingChunkLeaf(GameObject leafObject, Transform parent, int acquiredLevelOfDetail,
            List<string> calls) {
            LeafObject = leafObject;
            Parent = parent;
            AcquiredLevelOfDetail = acquiredLevelOfDetail;
            _calls = calls;
        }

        internal GameObject LeafObject { get; }

        internal Transform Parent { get; }

        internal int AcquiredLevelOfDetail { get; }

        internal Mesh Mesh { get; private set; }

        internal int MeshLevelOfDetail { get; private set; }

        internal Mesh TransitionMesh { get; private set; }

        internal Matrix4x4 VolumeMatrix { get; private set; }

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
    }
}
