// Copyright (c) Spellbound Studios. All rights reserved.

#if !SPELLBOUND_CORE
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Spellbound.GeoForge.Bootstrap {
    public sealed class GeoForgeBootstrap : EditorWindow {
        private const string WindowKey = "Spellbound.Bootstrap.WindowShown";
        private const string LineKey = "Spellbound.Bootstrap.GeoForge";
        private const string AddressFile = "FoundationAddress.txt";
        private const string Foundation = "com.spellboundstudios.core";

        [InitializeOnLoadMethod]
        private static void Offer() {
            var address = Address();

            if (!SessionState.GetBool(LineKey, false)) {
                SessionState.SetBool(LineKey, true);
                Debug.LogWarning("GeoForge needs " + Foundation + ". Install it from the Package Manager with "
                                 + "Install package from git URL: " + address);
            }

            if (SessionState.GetBool(WindowKey, false)) {
                return;
            }

            SessionState.SetBool(WindowKey, true);
            EditorApplication.delayCall += () => {
                var window = CreateInstance<GeoForgeBootstrap>();
                window.titleContent = new GUIContent(Foundation);
                window.ShowUtility();
            };
        }

        private static string Address() {
            var package = PackageInfo.FindForAssembly(typeof(GeoForgeBootstrap).Assembly);
            var path = package == null ? null : Path.Combine(package.resolvedPath, "Bootstrap", AddressFile);
            return path != null && File.Exists(path) ? File.ReadAllText(path).Trim() : Foundation;
        }

        private void CreateGUI() {
            var address = Address();
            var message = new Label("GeoForge needs " + Foundation + ", which this project does not hold yet.");
            message.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(message);
            var field = new TextField { value = address, isReadOnly = true };
            rootVisualElement.Add(field);
            rootVisualElement.Add(new Button(() => Client.Add(address)) { text = "Install " + Foundation });
        }
    }
}
#endif
