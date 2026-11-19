// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core.Authoring;
using Spellbound.Places;
using Spellbound.Places.Owners;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spellbound.GeoForge.PlacesMarker.Tests {
    public class MarkerLayerOwnerTests {
        private static readonly Vector3 Point = new(3f, 4f, 5f);

        private Scene _scene;

        [SetUp]
        public void OpenScene() {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void CloseScene() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void ThePreviewMakesOneMarkerWithoutACollider() {
            var layer = new MarkerLayer { LayerName = "marker", point = Point };
            var report = new ValidationReport("marker");

            new MarkerLayerOwner().Stamp(layer, new PlaceStampContext(report, preview: true));

            var root = FindRoot(MarkerLayerOwner.PreviewRootName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            var marker = root.transform.GetChild(0);
            Assert.That(marker.position, Is.EqualTo(Point));
            Assert.That(marker.GetComponent<Collider>(), Is.Null);
            Assert.That(marker.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(report.HasErrors, Is.False, report.ToText());
        }

        [Test]
        public void CaptureReadsTheMarkerPosition() {
            var layer = new MarkerLayer { LayerName = "marker", point = Point };
            var owner = new MarkerLayerOwner();
            var report = new ValidationReport("marker");
            owner.Stamp(layer, new PlaceStampContext(report, preview: true));

            var captured = owner.Capture(new PlaceCaptureContext("marker", report));

            Assert.That(captured.point, Is.EqualTo(Point));
            Assert.That(captured.LayerName, Is.EqualTo("marker"));
        }

        [Test]
        public void ThePointIsTheLayersOnePlacementPoint() {
            var layer = new MarkerLayer { LayerName = "marker", point = Point };

            var points = new List<PlacementPoint>(layer.PlacementPoints());

            Assert.That(points.Count, Is.EqualTo(1));
            Assert.That(points[0].Location, Is.EqualTo("marker[0]"));
            Assert.That(points[0].Position, Is.EqualTo(Point));
        }

        [Test]
        public void TheDefaultOwnerRegistryHoldsTheMarkerLayer() {
            Assert.That(PlaceLayerOwnerRegistry.Default.Has(typeof(MarkerLayer)), Is.True);
        }

        private GameObject FindRoot(string rootName) {
            foreach (var root in _scene.GetRootGameObjects()) {
                if (root.name == rootName) {
                    return root;
                }
            }

            return null;
        }
    }
}
