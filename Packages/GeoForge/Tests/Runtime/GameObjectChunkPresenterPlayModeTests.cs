// Copyright (c) Spellbound Studios. All rights reserved.

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spellbound.GeoForge.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spellbound.GeoForge.Tests {
    public class GameObjectChunkPresenterPlayModeTests {
        private const string PackageRoot = "Packages/com.spellboundstudios.geoforge/Runtime/";
        private const string ManagerPrefabPath = PackageRoot + "Prefabs/GeoForgeManager.prefab";
        private const string VolumePrefabPath = PackageRoot + "Prefabs/SimpleVolume.prefab";

        private const string RenderingProfilePath =
            PackageRoot + "Defaults/JobAndRenderProfiles/BarycentricProfile.asset";

        private const int FrameBudget = 1800;
        private const int SurfaceChecks = 200;
        private const float EdgeMargin = 0.05f;
        private const float WholeVoxelTolerance = 0.001f;

        private readonly List<GameObject> _created = new();

        private GeoVolume _volume;

        [UnityTearDown]
        public IEnumerator TearDown() {
            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.Destroy(_created[index]);

                yield return null;
            }

            _created.Clear();
            _volume = null;
        }

        [UnityTest]
        public IEnumerator TheSampleVolumeDrawsLeavesWithMeshesAndColliders() {
            yield return CreateMeshedSampleVolume();

            var drawnLeaves = DrawnLeaves();

            Assert.IsNotEmpty(drawnLeaves);

            foreach (var leaf in drawnLeaves)
                Assert.IsTrue(leaf.gameObject.activeInHierarchy, $"{leaf.name} is not visible.");
        }

        [UnityTest]
        public IEnumerator AManagerWithoutThePresenterComponentDrawsThroughTheRegisteredDefault() {
            yield return CreateMeshedSampleVolume(false);

            var drawnLeaves = DrawnLeaves();

            Assert.IsNotEmpty(drawnLeaves);
        }

        [UnityTest]
        public IEnumerator TheSurfaceCaseAtAMeshVertexHasTheSurface() {
            yield return CreateMeshedSampleVolume();

            var engine = _volume.GeoVolumeEngine;
            var checkedVertices = 0;

            foreach (var leaf in DrawnLeaves()) {
                foreach (var vertex in leaf.sharedMesh.vertices) {
                    var worldPosition = leaf.transform.TransformPoint(vertex);
                    var voxelPosition = engine.WorldToVoxelSpaceContinuous(worldPosition);

                    if (!LiesInsideOneVoxelEdge(voxelPosition))
                        continue;

                    Assert.IsTrue(engine.TryQuerySurfaceCase(worldPosition, out var surfaceCase));
                    Assert.IsTrue(surfaceCase.HasSurface,
                        $"The cell at {surfaceCase.CellOrigin} under a vertex of {leaf.name} has case " +
                        $"{surfaceCase.CaseCode}.");
                    Assert.AreEqual(Vector3Int.FloorToInt(voxelPosition), surfaceCase.CellOrigin);

                    if (++checkedVertices >= SurfaceChecks)
                        yield break;
                }
            }

            Assert.Greater(checkedVertices, 0);
        }

        private IEnumerator CreateMeshedSampleVolume(bool fromThePrefab = true) {
            Track(new GameObject("Level Of Detail Camera", typeof(Camera)));

            GeoForgeManager manager;

            if (fromThePrefab) {
                manager = Track(Object.Instantiate(Load<GameObject>(ManagerPrefabPath))).GetComponent<GeoForgeManager>();
            }
            else {
                var managerObject = Track(new GameObject("Scene Manager"));
                managerObject.SetActive(false);
                manager = managerObject.AddComponent<GeoForgeManager>();
                manager.LeafPrefab = Load<GameObject>(ManagerPrefabPath).GetComponent<GeoForgeManager>().LeafPrefab;
                manager.materialDatabase = Load<GameObject>(ManagerPrefabPath).GetComponent<GeoForgeManager>().materialDatabase;
                managerObject.SetActive(true);
            }

            manager.jobAndRenderProfile = Load<MaterialRenderingProfile>(RenderingProfilePath);

            Assert.IsInstanceOf<GameObjectChunkPresenter>(manager.Presenter);

            _volume = Track(Object.Instantiate(Load<GameObject>(VolumePrefabPath))).GetComponent<GeoVolume>();

            for (var frame = 0; frame < FrameBudget && !AllChunksMeshed(); frame++)
                yield return null;

            Assert.IsTrue(AllChunksMeshed(), $"The volume did not finish meshing within {FrameBudget} frames.");
        }

        private bool AllChunksMeshed() {
            var engine = _volume.GeoVolumeEngine;

            if (engine == null)
                return false;

            var chunks = engine.ChunkDict.Values;

            return chunks.Count == engine.ConfigBlob.Value.TotalChunks && chunks.All(chunk => chunk.MeshReady);
        }

        private MeshFilter[] DrawnLeaves() {
            return _volume.GetComponentsInChildren<MeshFilter>()
                .Where(filter => filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0)
                .Where(filter => filter.TryGetComponent<MeshCollider>(out var meshCollider) &&
                                 meshCollider.sharedMesh != null)
                .ToArray();
        }

        private static bool LiesInsideOneVoxelEdge(Vector3 voxelPosition) {
            var axesAlongTheEdge = 0;

            for (var axis = 0; axis < 3; axis++) {
                var fraction = voxelPosition[axis] - Mathf.Floor(voxelPosition[axis]);

                if (fraction > EdgeMargin && fraction < 1f - EdgeMargin)
                    axesAlongTheEdge++;
                else if (fraction > WholeVoxelTolerance && fraction < 1f - WholeVoxelTolerance)
                    return false;
            }

            return axesAlongTheEdge == 1;
        }

        private static T Load<T>(string path) where T : Object {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"Nothing of type {typeof(T).Name} at {path}.");

            return asset;
        }

        private GameObject Track(GameObject created) {
            _created.Add(created);

            return created;
        }
    }
}
#endif
