// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Entities;

namespace Spellbound.GeoForge {
    public readonly struct MarchRequest {
        public MarchRequest(
            MaterialRenderingProfile profile,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            int levelOfDetail,
            BlobAssetReference<McTablesBlobAsset> tables = default) {
            Profile = profile;
            ConfigBlob = configBlob;
            LevelOfDetail = levelOfDetail;
            Tables = tables;
        }

        public MaterialRenderingProfile Profile { get; }

        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; }

        public int LevelOfDetail { get; }

        public BlobAssetReference<McTablesBlobAsset> Tables { get; }
    }
}
