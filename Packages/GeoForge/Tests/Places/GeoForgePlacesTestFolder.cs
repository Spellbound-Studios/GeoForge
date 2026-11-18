// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEditor;

namespace Spellbound.GeoForge.Places.Tests {
    public sealed class GeoForgePlacesTestFolder : IDisposable {
        public const string Root = "Assets/SpellboundGeoForgePlacesTests";

        public string Folder { get; }

        public GeoForgePlacesTestFolder() {
            if (!AssetDatabase.IsValidFolder(Root)) {
                AssetDatabase.CreateFolder("Assets", Root.Substring("Assets/".Length));
            }

            var name = "Run" + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder(Root, name);
            Folder = Root + "/" + name;
        }

        public string Path(string fileName) {
            return Folder + "/" + fileName;
        }

        public void Dispose() {
            AssetDatabase.DeleteAsset(Folder);

            if (AssetDatabase.IsValidFolder(Root)
                && AssetDatabase.FindAssets(string.Empty, new[] { Root }).Length == 0) {
                AssetDatabase.DeleteAsset(Root);
            }
        }
    }
}
