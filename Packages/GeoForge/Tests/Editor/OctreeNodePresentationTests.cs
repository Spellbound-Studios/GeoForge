// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core.Tooling;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class OctreeNodePresentationTests {
        private const int CubesPerMarch = 16;

        private readonly List<Object> _created = new();
        private readonly List<OctreeNode> _nodes = new();

        private GeoChunkDouble _chunk;
        private Transform _chunkTransform;
        private BlobAssetReference<VolumeConfigBlobAsset> _configBlob;
        private bool _hadManager;
        private GeoForgeManager _manager;
        private GeoForgeManager _previousManager;
        private RecordingChunkPresenter _presenter;
        private GeoVolumeDouble _volume;

        [SetUp]
        public void SetUp() {
            _hadManager = SingletonManager.TryGetSingletonInstance(out _previousManager);

            _manager = Track(new GameObject("GeoForge Manager")).AddComponent<GeoForgeManager>();
            SingletonManager.RegisterSingleton(_manager);
            _presenter = new RecordingChunkPresenter();
            _manager.Presenter = _presenter;

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = CubesPerMarch;
            config.levelsOfDetail = 1;
            config.maxChunkSize = CubesPerMarch;
            config.chunkSize = CubesPerMarch;
            config.sizeInChunks = Vector3Int.one;
            _configBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);

            var volumeObject = Track(new GameObject("Volume"));
            volumeObject.transform.position = new Vector3(3f, -2f, 5f);
            _volume = volumeObject.AddComponent<GeoVolumeDouble>();
            _volume.ConfigBlob = _configBlob;

            _chunkTransform = new GameObject("Chunk").transform;
            _chunkTransform.SetParent(volumeObject.transform, false);
            _chunk = new GeoChunkDouble(_chunkTransform);
        }

        [TearDown]
        public void TearDown() {
            foreach (var node in _nodes)
                node.Dispose();

            _nodes.Clear();

            if (_configBlob.IsCreated)
                _configBlob.Dispose();

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();

            if (_hadManager)
                SingletonManager.RegisterSingleton(_previousManager);
            else
                SingletonManager.UnregisterSingleton<GeoForgeManager>();
        }

        [Test]
        public void ChunkPresenter_CustomImplementation() {
            var node = CreateNode();

            ApplyTriangle(node);

            CollectionAssert.AreEqual(new[] { "Acquire", "SetTransitionMesh", "SetVolumeMatrix", "SetMesh" },
                _presenter.Calls);
            Assert.AreEqual(1, _presenter.Acquired.Count);
            var leaf = _presenter.Acquired[0];
            Assert.AreSame(_chunkTransform, leaf.Parent);
            Assert.AreEqual(0, leaf.AcquiredLevelOfDetail);
            Assert.AreEqual($"LeafSize {CubesPerMarch} at 0, 0, 0", leaf.Name);
            Assert.IsNotNull(leaf.Mesh);
            Assert.AreEqual(3, leaf.Mesh.vertexCount);
            Assert.AreEqual(3, leaf.Mesh.GetIndexCount(0));
            Assert.AreEqual(0, leaf.MeshLevelOfDetail);
            Assert.IsNotNull(leaf.TransitionMesh);
            Assert.AreNotSame(leaf.Mesh, leaf.TransitionMesh);
            Assert.AreEqual(_volume.transform.worldToLocalMatrix, leaf.VolumeMatrix);
            Assert.AreEqual(6, _chunk.NewLeafBroadcasts);

            ApplyTriangle(node);

            Assert.AreEqual(1, _presenter.Acquired.Count);
            Assert.AreEqual("SetMesh", _presenter.Calls[_presenter.Calls.Count - 1]);
            Assert.AreEqual(6, _chunk.NewLeafBroadcasts);

            var mesh = leaf.Mesh;
            var transitionMesh = leaf.TransitionMesh;
            node.Dispose();

            Assert.AreEqual("Release", _presenter.Calls[_presenter.Calls.Count - 1]);
            CollectionAssert.AreEqual(new IChunkLeaf[] { leaf }, _presenter.Released);
            Assert.IsTrue(mesh == null);
            Assert.IsTrue(transitionMesh == null);
            CollectionAssert.DoesNotContain(_presenter.Calls, "SetVisible");
        }

        [Test]
        public void AnEmptyResultReleasesTheLeafAndRecordsTheAddressAsKnownEmpty() {
            var node = CreateNode();
            ApplyTriangle(node);
            var leaf = _presenter.Acquired[0];

            ApplyEmpty(node);

            Assert.AreEqual("Release", _presenter.Calls[_presenter.Calls.Count - 1]);
            CollectionAssert.AreEqual(new IChunkLeaf[] { leaf }, _presenter.Released);
            CollectionAssert.AreEqual(new[] { (0, Vector3Int.zero) }, _chunk.KnownEmptyAddresses);
            Assert.IsTrue(leaf.Mesh == null);

            node.Dispose();

            Assert.AreEqual(1, _presenter.Released.Count);
        }

        [Test]
        public void MovingTheVolumeSendsTheLeafTheNewVolumeMatrix() {
            var node = CreateNode();
            ApplyTriangle(node);
            var leaf = _presenter.Acquired[0];

            _volume.transform.SetPositionAndRotation(new Vector3(-7f, 1f, 2f), Quaternion.Euler(0f, 30f, 0f));
            node.ValidateMaterial();

            Assert.AreEqual("SetVolumeMatrix", _presenter.Calls[_presenter.Calls.Count - 1]);
            Assert.AreEqual(_volume.transform.worldToLocalMatrix, leaf.VolumeMatrix);
        }

        [Test]
        public void TransitionResultsFillTheTransitionMeshTheLeafWasGiven() {
            var node = CreateNode();
            ApplyTriangle(node);
            var leaf = _presenter.Acquired[0];

            using var vertices = TriangleVertices();
            using var triangles = TriangleIndices();
            using var ranges = new NativeArray<int2>(6, Allocator.Temp);

            node.ApplyTransitionMarchResults(vertices, triangles, ranges);

            Assert.AreEqual(3, leaf.TransitionMesh.vertexCount);
            Assert.AreEqual(1, _presenter.Acquired.Count);
        }

        [Test]
        public void WithoutAPresenterMarchResultsAreDiscardedAndEmptyAddressesAreStillRecorded() {
            _manager.Presenter = null;
            var node = CreateNode();

            ApplyTriangle(node);

            using (var vertices = TriangleVertices())
            using (var triangles = TriangleIndices())
            using (var ranges = new NativeArray<int2>(6, Allocator.Temp)) {
                node.ApplyTransitionMarchResults(vertices, triangles, ranges);
            }

            Assert.AreEqual(0, _chunk.NewLeafBroadcasts);
            CollectionAssert.IsEmpty(_chunk.KnownEmptyAddresses);
            Assert.AreEqual(0, _chunkTransform.childCount);

            ApplyEmpty(node);

            CollectionAssert.AreEqual(new[] { (0, Vector3Int.zero) }, _chunk.KnownEmptyAddresses);
            CollectionAssert.IsEmpty(_presenter.Calls);
        }

        private OctreeNode CreateNode() {
            var node = new OctreeNode(Vector3Int.zero, 0, _chunk, _volume);
            _nodes.Add(node);

            return node;
        }

        private static void ApplyTriangle(OctreeNode node) {
            using var vertices = TriangleVertices();
            using var triangles = TriangleIndices();

            node.ApplyMarchResults(vertices, triangles, new Bounds(new Vector3(0.5f, 0.5f, 0f), Vector3.one));
        }

        private static void ApplyEmpty(OctreeNode node) {
            using var vertices = new NativeList<MeshingVertexData>(Allocator.Temp);
            using var triangles = new NativeList<int>(Allocator.Temp);

            node.ApplyMarchResults(vertices, triangles, default);
        }

        private static NativeList<MeshingVertexData> TriangleVertices() {
            var vertices = new NativeList<MeshingVertexData>(3, Allocator.Temp);
            var normal = new float3(0f, 0f, -1f);
            var white = new Color32(255, 255, 255, 255);
            vertices.Add(new MeshingVertexData(new float3(0f, 0f, 0f), normal, white, white));
            vertices.Add(new MeshingVertexData(new float3(0f, 1f, 0f), normal, white, white));
            vertices.Add(new MeshingVertexData(new float3(1f, 0f, 0f), normal, white, white));

            return vertices;
        }

        private static NativeList<int> TriangleIndices() {
            var triangles = new NativeList<int>(3, Allocator.Temp);
            triangles.Add(0);
            triangles.Add(1);
            triangles.Add(2);

            return triangles;
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
