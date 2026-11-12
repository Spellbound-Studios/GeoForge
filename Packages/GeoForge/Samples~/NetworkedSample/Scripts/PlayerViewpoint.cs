// Copyright (c) Spellbound Studios. All rights reserved.

using PurrNet;
using Spellbound.Core.Logging;
using UnityEngine;

namespace Spellbound.GeoForge.Sample4 {
    public class PlayerViewpoint : NetworkBehaviour {
        private NetworkedVolume _volume;

        protected virtual void Update() {
            if (!isOwner)
                return;

            var camera = Camera.main;

            if (camera != null)
                transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) {
            if (!asServer)
                return;

            if (_volume != null)
                _volume.ForgetPlayer(transform);

            if (!newOwner.HasValue)
                return;

            _volume = FindAnyObjectByType<NetworkedVolume>();

            if (_volume == null) {
                Log.Error($"{name}: no {nameof(NetworkedVolume)} is in the scene, so the player opens no region.",
                    this);

                return;
            }

            if (!_volume.TrackPlayer(newOwner.Value, transform))
                Log.Error($"{name}: player {newOwner.Value} could not be tracked on the volume's world.", this);
        }

        protected override void OnDespawned(bool asServer) {
            if (asServer && _volume != null)
                _volume.ForgetPlayer(transform);

            _volume = null;
        }
    }
}
