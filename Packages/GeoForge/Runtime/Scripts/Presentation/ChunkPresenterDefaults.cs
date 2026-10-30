// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class ChunkPresenterDefaults {
        private static Func<GeoForgeManager, IChunkPresenter> _factory;

        public static Func<GeoForgeManager, IChunkPresenter> Factory {
            get => _factory;
            set => _factory = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            _factory = null;
        }

        internal static IChunkPresenter Create(GeoForgeManager manager) {
            return _factory?.Invoke(manager);
        }
    }
}
