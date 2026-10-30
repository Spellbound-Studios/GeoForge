// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spellbound.GeoForge.Presentation;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class GameObjectChunkPresenterTests {
        private const string PoolParentName = "OctreeLeafPool";

        private readonly List<Object> _created = new();

        private Transform _chunk;
        private GeoForgeManager _manager;
        private GameObjectChunkPresenter _presenter;

        [SetUp]
        public void SetUp() {
            var managerObject = Track(new GameObject("GeoForge Manager"));
            _manager = managerObject.AddComponent<GeoForgeManager>();
            _presenter = managerObject.AddComponent<GameObjectChunkPresenter>();
            _manager.LeafPrefab = CreateLeafTemplate();
            _chunk = Track(new GameObject("Chunk")).transform;
        }

        [TearDown]
        public void TearDown() {
            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void AcquiredLeavesTakeTheMeshAndReleasedLeavesAreReused() {
            var first = _presenter.Acquire(_chunk, 0);
            var second = _presenter.Acquire(_chunk, 1);
            var firstObjects = ObjectsOf(first);
            var secondObjects = ObjectsOf(second);

            Assert.AreSame(_chunk, first.Transform.parent);
            Assert.AreSame(first.Transform, firstObjects[1].transform.parent);
            CollectionAssert.IsEmpty(firstObjects.Intersect(secondObjects));

            var mesh = Track(CreateTriangleMesh());
            first.SetMesh(mesh, 2);

            var leafObject = first.Transform.gameObject;
            Assert.AreSame(mesh, leafObject.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreSame(mesh, leafObject.GetComponent<MeshCollider>().sharedMesh);
            var behaviour = leafObject.GetComponent<RecordingLeafBehaviour>();
            Assert.AreEqual(1, behaviour.MeshUpdates);
            Assert.AreEqual(2, behaviour.LastLevelOfDetail);

            _presenter.Release(first);
            _presenter.Release(second);

            Assert.IsNull(leafObject.GetComponent<MeshFilter>().sharedMesh);
            Assert.IsNull(leafObject.GetComponent<MeshCollider>().sharedMesh);

            foreach (var released in firstObjects.Concat(secondObjects)) {
                Assert.IsFalse(released.activeSelf);
                Assert.AreEqual(PoolParentName, released.transform.parent.name);
                Assert.AreSame(_manager.transform, released.transform.parent.parent);
            }

            var third = _presenter.Acquire(_chunk, 0);
            var fourth = _presenter.Acquire(_chunk, 0);
            var reused = ObjectsOf(third).Concat(ObjectsOf(fourth)).ToArray();

            CollectionAssert.AreEquivalent(firstObjects.Concat(secondObjects), reused);
            Assert.AreSame(_chunk, third.Transform.parent);
            Assert.AreSame(_chunk, fourth.Transform.parent);

            foreach (var acquired in reused)
                Assert.IsTrue(acquired.activeSelf);
        }

        [Test]
        public void TheVolumeMatrixReachesBothRenderers() {
            var leaf = _presenter.Acquire(_chunk, 0);
            var worldToLocal = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.identity, Vector3.one);

            leaf.SetVolumeMatrix(worldToLocal);

            foreach (var leafPart in ObjectsOf(leaf)) {
                var block = new MaterialPropertyBlock();
                leafPart.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.AreEqual(worldToLocal, block.GetMatrix("_WorldToLocal"));
            }
        }

        private static GameObject[] ObjectsOf(IChunkLeaf leaf) {
            return new[] { leaf.Transform.gameObject, leaf.Transform.GetChild(0).gameObject };
        }

        private GameObject CreateLeafTemplate() {
            var template = Track(new GameObject("Leaf Template"));
            template.AddComponent<MeshFilter>();
            template.AddComponent<MeshRenderer>();
            template.AddComponent<MeshCollider>();
            template.AddComponent<RecordingLeafBehaviour>();

            return template;
        }

        private static Mesh CreateTriangleMesh() {
            var mesh = new Mesh();
            mesh.SetVertices(new[] { Vector3.zero, Vector3.up, Vector3.right });
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);

            return mesh;
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
