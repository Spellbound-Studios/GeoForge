// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingCollisionLeaf : IChunkCollisionLeaf {
        private readonly List<string> _calls;

        internal RecordingCollisionLeaf(GameObject leafObject, Transform parent, int acquiredLevelOfDetail,
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

        internal Matrix4x4 VolumeMatrix { get; private set; }

        internal bool ThrowsOnSetMesh { get; set; }

        public bool HasMesh => Mesh != null;

        public void SetMesh(Mesh mesh) {
            _calls.Add(nameof(SetMesh));

            if (ThrowsOnSetMesh)
                throw new InvalidOperationException(nameof(ThrowsOnSetMesh));

            Mesh = mesh;
        }

        public void SetVolumeMatrix(Matrix4x4 worldToLocal) {
            _calls.Add(nameof(SetVolumeMatrix));
            VolumeMatrix = worldToLocal;
        }
    }
}
