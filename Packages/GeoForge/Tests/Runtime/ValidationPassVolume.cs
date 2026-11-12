// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public sealed class ValidationPassVolume : MonoBehaviour, IGeoVolume {
        public ValidationPassVolume DestroysOnFirstValidation { get; set; }

        public int Validations { get; private set; }

        public int ValidationsAfterDestroy { get; private set; }

        public GeoVolumeEngine GeoVolumeEngine => null;

        public Vector2[] ViewDistanceLodRanges => Array.Empty<Vector2>();

        public bool IsMoving { get; set; }

        public bool IsPrimaryTerrain { get; set; }

        public void ResetEditedChunksToProcedural() { }

        Awaitable IGeoVolume.ValidateChunkLods() {
            Validations++;

            if (this == null)
                ValidationsAfterDestroy++;

            if (Validations == 1 && DestroysOnFirstValidation != null)
                Object.DestroyImmediate(DestroysOnFirstValidation.gameObject);

            var completed = new AwaitableCompletionSource();
            completed.SetResult();

            return completed.Awaitable;
        }
    }
}
