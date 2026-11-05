// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spellbound.GeoForge.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

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
            var first = Acquire(0, out var firstObjects);
            var second = Acquire(1, out var secondObjects);

            Assert.AreSame(_chunk, firstObjects[0].transform.parent);
            Assert.AreSame(firstObjects[0].transform, firstObjects[1].transform.parent);
            CollectionAssert.IsEmpty(firstObjects.Intersect(secondObjects));

            var mesh = Track(CreateTriangleMesh());
            first.SetMesh(mesh, 2);

            var leafObject = firstObjects[0];
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

            Acquire(0, out var thirdObjects);
            Acquire(0, out var fourthObjects);
            var reused = thirdObjects.Concat(fourthObjects).ToArray();

            CollectionAssert.AreEquivalent(firstObjects.Concat(secondObjects), reused);
            Assert.AreSame(_chunk, thirdObjects[0].transform.parent);
            Assert.AreSame(_chunk, fourthObjects[0].transform.parent);

            foreach (var acquired in reused)
                Assert.IsTrue(acquired.activeSelf);
        }

        [Test]
        public void TheVolumeMatrixReachesBothRenderers() {
            var leaf = Acquire(0, out var leafObjects);
            var worldToLocal = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.identity, Vector3.one);

            leaf.SetVolumeMatrix(worldToLocal);

            foreach (var leafPart in leafObjects) {
                var block = new MaterialPropertyBlock();
                leafPart.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.AreEqual(worldToLocal, block.GetMatrix("_WorldToLocal"));
            }
        }

        [Test]
        public void AManagerWithoutALeafPrefabRefusesOnceAndHandsOutNoLeaf() {
            _manager.LeafPrefab = null;
            LogAssert.Expect(LogType.Error,
                new Regex(Regex.Escape(_manager.name) + ".*" + nameof(GeoForgeManager.LeafPrefab)));

            Assert.IsNull(_presenter.Acquire(_chunk, 0));
            Assert.IsNull(_presenter.Acquire(_chunk, 1));
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(0, _chunk.childCount);
        }

        [Test]
        public void AcquiredLeavesAreNamedFromTheirLevelOfDetailInTheOrderHandedOut() {
            Acquire(2, out var firstObjects);
            Acquire(0, out var secondObjects);

            Assert.AreEqual("Leaf 1 at level of detail 2", firstObjects[0].name);
            Assert.AreEqual("Leaf 1 at level of detail 2 Transition", firstObjects[1].name);
            Assert.AreEqual("Leaf 2 at level of detail 0", secondObjects[0].name);
            Assert.AreEqual("Leaf 2 at level of detail 0 Transition", secondObjects[1].name);
        }

        private IChunkLeaf Acquire(int levelOfDetail, out GameObject[] leafObjects) {
            var childCount = _chunk.childCount;
            var leaf = _presenter.Acquire(_chunk, levelOfDetail);

            Assert.AreEqual(childCount + 1, _chunk.childCount);
            var leafTransform = _chunk.GetChild(childCount);
            Assert.AreEqual(1, leafTransform.childCount);
            leafObjects = new[] { leafTransform.gameObject, leafTransform.GetChild(0).gameObject };

            return leaf;
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
