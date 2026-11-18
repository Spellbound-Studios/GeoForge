// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.IO;
using Spellbound.GeoForge.Places;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace PlacesConversion {
    public static class CaptureTextConverter {
        public static PlaceDefinition Convert(string captureTextPath, string placeAssetPath, string layerName) {
            if (string.IsNullOrEmpty(captureTextPath)) {
                throw new ArgumentException("A capture text path is required.", nameof(captureTextPath));
            }

            if (string.IsNullOrEmpty(placeAssetPath)) {
                throw new ArgumentException("A place asset path is required.", nameof(placeAssetPath));
            }

            if (string.IsNullOrEmpty(layerName)) {
                throw new ArgumentException("A layer name is required.", nameof(layerName));
            }

            var capture = CaptureText.Parse(File.ReadAllText(captureTextPath));
            var layer = new VoxelLayer { LayerName = layerName };
            var chunk = new VoxelLayerChunk(Vector3Int.zero, capture.Runs);

            layer.SetChunks(CaptureText.CaptureChunkSize, Vector3Int.one, new[] { chunk });

            var place = ScriptableObject.CreateInstance<PlaceDefinition>();
            place.AddLayer(layer);

            AssetDatabase.CreateAsset(place, placeAssetPath);
            AssetDatabase.SaveAssets();
            return place;
        }
    }
}