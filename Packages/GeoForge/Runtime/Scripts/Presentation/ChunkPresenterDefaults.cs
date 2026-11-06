// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class ChunkPresenterDefaults {
        private static Func<GeoForgeManager, IChunkCollisionPresenter> _defaultCollisionFactory;
        private static Func<GeoForgeManager, IChunkPresenter> _defaultFactory;

        public static Func<GeoForgeManager, IChunkPresenter> Factory { get; set; }

        public static Func<GeoForgeManager, IChunkCollisionPresenter> CollisionFactory { get; set; }

        internal static void RegisterDefault(Func<GeoForgeManager, IChunkPresenter> factory) {
            _defaultFactory = factory;
            Factory = factory;
        }

        internal static void RegisterDefaultCollision(Func<GeoForgeManager, IChunkCollisionPresenter> factory) {
            _defaultCollisionFactory = factory;
            CollisionFactory = factory;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() {
            Factory = _defaultFactory;
            CollisionFactory = _defaultCollisionFactory;
        }

        internal static IChunkPresenter Create(GeoForgeManager manager) {
            return Factory?.Invoke(manager);
        }

        internal static IChunkCollisionPresenter CreateCollision(GeoForgeManager manager) {
            return CollisionFactory?.Invoke(manager);
        }
    }
}
