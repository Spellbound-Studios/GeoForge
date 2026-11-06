// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    public struct MarchOutput : IDisposable {
        private NativeArray<MeshingVertexData> _vertices;
        private NativeArray<int> _indices;

        internal MarchOutput(NativeArray<MeshingVertexData> vertices, NativeArray<int> indices, Bounds bounds) {
            _vertices = vertices;
            _indices = indices;
            Bounds = bounds;
        }

        public NativeArray<MeshingVertexData> Vertices => _vertices;

        public NativeArray<int> Indices => _indices;

        public Bounds Bounds { get; }

        public void Dispose() {
            if (_vertices.IsCreated)
                _vertices.Dispose();

            if (_indices.IsCreated)
                _indices.Dispose();

            _vertices = default;
            _indices = default;
        }
    }
}
