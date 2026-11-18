// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.IO;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    public static class VoxelRunStores {
        public static string PathBeside(string placeAssetPath, string layerName) {
            if (string.IsNullOrEmpty(placeAssetPath)) {
                throw new ArgumentException("A place asset path is required.", nameof(placeAssetPath));
            }

            if (string.IsNullOrEmpty(layerName)) {
                throw new ArgumentException("A layer name is required.", nameof(layerName));
            }

            return Path.ChangeExtension(placeAssetPath, null) + "." + layerName + ".runs.asset";
        }

        public static VoxelLayer Write(PlaceDefinition place, VoxelLayer found, VoxelLayer produced) {
            if (place == null) {
                throw new ArgumentNullException(nameof(place));
            }

            if (produced == null) {
                throw new ArgumentNullException(nameof(produced));
            }

            if (found != null) {
                found.SetChunks(produced.ChunkSize, produced.ChunkExtent, produced.Chunks);

                if (found.Store != null) {
                    EditorUtility.SetDirty(found.Store);
                }

                return found;
            }

            var store = ScriptableObject.CreateInstance<VoxelRunStore>();
            store.SetChunks(produced.Chunks);
            AssetDatabase.CreateAsset(store, PathBeside(AssetDatabase.GetAssetPath(place), produced.LayerName));
            produced.UseStore(store);
            place.AddLayer(produced);

            return produced;
        }
    }
}
