// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public sealed class GeoVolumeDouble : MonoBehaviour, IGeoVolume {
        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; set; }

        public Transform VolumeTransform => transform;

        public GeoVolumeEngine GeoVolumeEngine { get; set; }

        public Vector2[] ViewDistanceLodRanges { get; set; } = Array.Empty<Vector2>();

        public bool IsMoving { get; set; }

        public bool IsPrimaryTerrain { get; set; }

        public void ResetEditedChunksToProcedural() { }
    }
}
