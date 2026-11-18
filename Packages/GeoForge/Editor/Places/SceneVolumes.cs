// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places {
    public static class SceneVolumes {
        public static IGeoVolume FirstWithEngine() {
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) {
                if (behaviour is IGeoVolume volume && volume.GeoVolumeEngine != null) {
                    return volume;
                }
            }

            return null;
        }
    }
}
