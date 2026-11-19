// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Places.Owners;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.PlacesMarker {
    [PlaceLayerOwner]
    public sealed class MarkerLayerOwner : IPlaceLayerOwner<MarkerLayer> {
        public const string PreviewRootName = "Marker Preview";
        public const float MarkerDiameter = 0.6f;

        public MarkerLayerOwner() {
        }

        public void Stamp(MarkerLayer layer, PlaceStampContext context) {
            var root = FindRoot();

            if (root == null) {
                root = new GameObject(PreviewRootName);
            }

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = layer.LayerName;
            marker.transform.SetParent(root.transform, false);
            marker.transform.position = layer.point;
            marker.transform.localScale = Vector3.one * MarkerDiameter;

            if (context.Preview) {
                Object.DestroyImmediate(marker.GetComponent<Collider>());
            }
        }

        public MarkerLayer Capture(PlaceCaptureContext context) {
            var layer = new MarkerLayer { LayerName = context.LayerName };
            var root = FindRoot();

            if (root != null && root.transform.childCount > 0) {
                layer.point = root.transform.GetChild(0).position;
            }

            return layer;
        }

        private static GameObject FindRoot() {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) {
                if (root.name == PreviewRootName) {
                    return root;
                }
            }

            return null;
        }
    }
}
