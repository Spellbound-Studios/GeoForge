// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using PurrNet;
using PurrNet.Transports;
using Spellbound.Core;
using Spellbound.Core.Logging;
using Spellbound.Core.Observations;
using Spellbound.Core.Simulation;
using Spellbound.Core.Viewers;
using Spellbound.GeoForge.Networking;
using Spellbound.Networking;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Sample4 {
    public class NetworkedVolume : GeoVolume {
        [Tooltip("The network manager this volume's edits replicate through. Left empty, the volume edits locally.")]
        [SerializeField]
        private NetworkManager networkManager;

        [Tooltip("The replication settings of this volume's world, with the layer its edits travel on.")]
        [SerializeField]
        private ReplicationSettings replicationSettings;

        [Tooltip("The index of the layer in the replication settings that carries the voxel edits.")]
        [SerializeField]
        private int editLayer;

        [Tooltip("The index of this volume's world on the network manager, unique per manager.")]
        [SerializeField]
        private byte worldIndex = 1;

        [Tooltip("The id every region's voxel edit source carries; equal on every peer.")]
        [SerializeField]
        private uint editSourceId = 1;

        [Tooltip("The fingerprint of the procedural terrain the edits apply over; equal on every peer.")]
        [SerializeField]
        private ulong terrainFingerprint = 1;

        [Tooltip("The collision radius of a player's viewer, in metres.")]
        [SerializeField]
        private float viewerCollisionRadius = 1f;

        [Tooltip("The detail radius of a player's viewer, in metres.")]
        [SerializeField]
        private float viewerDetailRadius = 1f;

        private readonly Dictionary<Transform, ViewerId> _viewpoints = new();
        private ObservationContext _observations;
        private TickClock _clock;
        private bool _listening;

        public VoxelEditStores VoxelEditStores { get; private set; }

        public WorldNetworking Networking { get; private set; }

        protected override void Awake() {
            _observations = CoreKit.Observations.CreateContext("geoforge_networked_volume_" + GetInstanceID());
            var settings = new VoxelEditSourceSettings(terrainFingerprint, editSourceId, null, 0, EditCellSize());
            VoxelEditStores = GeoForgeNetworkingKit.CreateEditStores(this, settings, _observations);
            UseEditStores(VoxelEditStores);
            base.Awake();
        }

        protected virtual void Start() {
            if (networkManager == null)
                return;

            if (replicationSettings == null) {
                Log.Error($"{name}: no {nameof(ReplicationSettings)} is assigned, so its edits stay local.", this);

                return;
            }

            var rate = TickRate.PerSecond(networkManager.tickRate);
            _clock = CoreKit.Time.StartClock(rate, _observations);
            var services = new WorldNetworkingServices {
                Clock = _clock,
                Ledger = CoreKit.Intents.CreateLedger(rate, _observations),
                Viewers = new ViewerRegistry(),
                Binder = new VoxelRegionBinder(VoxelEditStores),
                Scene = gameObject.scene,
                WorldIndex = worldIndex
            };
            Networking = NetworkingPurrNetKit.CreateWorldNetworking(replicationSettings, networkManager, in services,
                _observations);

            if (Networking == null)
                return;

            networkManager.onServerConnectionState += OnConnectionState;
            networkManager.onClientConnectionState += OnConnectionState;
            _listening = true;
        }

        protected override void Update() {
            base.Update();

            if (Networking == null)
                return;

            var viewers = Networking.Services.Viewers;

            foreach (var viewpoint in _viewpoints)
                viewers.Move(viewpoint.Value, viewpoint.Key.position);
        }

        protected override void OnDestroy() {
            StopNetworking();
            base.OnDestroy();
            VoxelEditStores?.Dispose();
            _observations?.Dispose();
        }

        public bool TrackPlayer(PlayerID player, Transform viewpoint) {
            if (Networking == null || viewpoint == null || _viewpoints.ContainsKey(viewpoint) ||
                !Networking.Participants.Contains(player))
                return false;

            var viewer = Networking.Services.Viewers.Add(viewpoint.position, viewerCollisionRadius,
                viewerDetailRadius);
            Networking.Streamer.Track(viewer, Networking.Participants.Of(player));
            _viewpoints.Add(viewpoint, viewer);

            return true;
        }

        public void ForgetPlayer(Transform viewpoint) {
            if (viewpoint == null || !_viewpoints.Remove(viewpoint, out var viewer) || Networking == null)
                return;

            Networking.Streamer.Forget(viewer);
            Networking.Services.Viewers.Remove(viewer);
        }

        private void OnConnectionState(ConnectionState state) {
            if (state == ConnectionState.Disconnecting || state == ConnectionState.Disconnected)
                StopNetworking();
        }

        private void StopNetworking() {
            if (_listening && networkManager != null) {
                networkManager.onServerConnectionState -= OnConnectionState;
                networkManager.onClientConnectionState -= OnConnectionState;
            }

            _listening = false;
            _viewpoints.Clear();
            Networking?.Dispose();
            Networking = null;
            CoreKit.Time.StopClock(_clock);
            _clock = null;
        }

        private float EditCellSize() {
            if (replicationSettings == null || replicationSettings.layers == null || editLayer < 0 ||
                editLayer >= replicationSettings.layers.Count)
                return 0f;

            return replicationSettings.layers[editLayer].cellSize;
        }
    }
}
