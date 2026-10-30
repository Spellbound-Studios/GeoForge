// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public sealed class GeoVolumeDouble : MonoBehaviour, IGeoVolume {
        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; set; }

        public Transform VolumeTransform => transform;

        public GeoVolumeEngine GeoVolumeEngine => null;

        public Vector2[] ViewDistanceLodRanges => Array.Empty<Vector2>();

        public Transform LodTarget => null;

        public bool IsMoving { get; set; }

        public bool IsPrimaryTerrain { get; set; }

        public void ResetEditedChunksToProcedural() { }
    }
}
