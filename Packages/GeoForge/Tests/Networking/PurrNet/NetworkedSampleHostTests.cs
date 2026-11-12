// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using PurrNet;
using PurrNet.Transports;
using Spellbound.Core;
using Spellbound.Core.Observations;
using Spellbound.Networking;
using Spellbound.Networking.Replication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Spellbound.GeoForge.Networking.Tests {
    public class NetworkedSampleHostTests {
        private const string SampleSceneName = "NetworkedGeoForge";
        private const string SampleVolumeName = "MyVolume";
        private const float TimeoutSeconds = 60f;
        private const float DigRadius = 3f;
        private const int DigStrength = 255;
        private const int MessageBytes = 1 << 17;
        private const int LodPassFrames = 120;
        private const float SurfaceSearchTop = 120f;
        private const float SurfaceSearchBottom = -400f;
        private static readonly Vector2 DigColumn = new(2.5f, 2.5f);

        private GameObject _secondClient;
        private GeoVolume _host;
        private WorldNetworking _world;
        private ObservationContext _observations;
        private RegionClient _client;
        private bool _autoStartDisabled;

        [UnityTearDown]
        public IEnumerator StopTheHost() {
            _client?.Dispose();
            _client = null;
            var manager = NetworkManager.main;

            if (manager) {
                if (manager.clientState != ConnectionState.Disconnected)
                    manager.StopClient();

                if (manager.serverState != ConnectionState.Disconnected)
                    manager.StopServer();

                yield return PollUntil(() => manager.serverState == ConnectionState.Disconnected &&
                                             manager.clientState == ConnectionState.Disconnected, "the host to stop");
                Object.Destroy(manager.gameObject);
            }

            var disposedOnStop = _world == null || _world.IsDisposed;
            _world = null;

            if (_secondClient)
                Object.Destroy(_secondClient);

            _secondClient = null;

            if (_host)
                Object.Destroy(_host.gameObject);

            _host = null;

            for (var frame = 0; frame < LodPassFrames; frame++)
                yield return null;

            var empty = SceneManager.CreateScene("Networked Sample Teardown " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);

            for (var index = SceneManager.sceneCount - 1; index >= 0; index--) {
                var scene = SceneManager.GetSceneAt(index);

                if (scene != empty && scene.name == SampleSceneName)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }

            yield return null;
            _observations?.Dispose();
            _observations = null;

            if (_autoStartDisabled)
                NetworkManager.EnableFlags();

            _autoStartDisabled = false;

            Assert.IsTrue(disposedOnStop, "the sample volume disposes its world when the host stops");
        }

        [UnityTest]
        public IEnumerator Dig_OnTheHost_ReachesASecondClientThroughTheHubsFlush() {
            var samplePaths = SamplePaths();

            if (samplePaths.Scene == null)
                Assert.Ignore("The Networked Digging a Hole sample is not imported. Import it from the Package " +
                              "Manager's Samples tab, or copy the package's NetworkedSample folder to " +
                              "Assets/Samples/Spellbound.GeoForge/<version>/Networked Digging a Hole.");

            NetworkManager.DisableFlags();
            _autoStartDisabled = true;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(samplePaths.Scene,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return PollUntil(() => NetworkManager.main, "the sample's network manager");
            var manager = NetworkManager.main;
            var transport = manager.GetComponent<UDPTransport>();
            Assert.IsNotNull(transport, "the sample's network manager uses the UDP transport");
            transport.serverPort = FreePort();
            manager.StartHost();
            yield return PollUntil(() => manager.isHost && manager.isLocalPlayerReady, "the sample's host to start");
            var host = FindSampleVolume();
            Assert.IsNotNull(host, "the sample scene holds a volume with voxel edit stores");
            _host = host;
            var hostStores = (VoxelEditStores)host.EditStores;
            IGeoVolume hostVolume = host;
            var digPoint = Vector3.zero;
            yield return PollUntil(() => AllChunksFilled(hostVolume), "the host's chunks to fill");
            Assert.IsTrue(TryFindSurface(hostVolume, out digPoint), "the host's volume has a surface to dig");

            var chunk = hostVolume.GetCoordByVoxelPosition(hostVolume.WorldToVoxelSpace(digPoint));
            var key = hostStores.ChunkRegions.RegionOf(chunk);
            ReplicatedRegion region = null;
            yield return PollUntil(() => TryFindRegion(key, manager.localPlayer, out region),
                "the region " + key + " to open for the host's player");
            Assert.AreEqual(WritePolicy.Owner, region.Layer.WritePolicy);
            _world = region.World;
            Assert.AreSame(hostStores.SourceFor(key), region.Hub.Region.Sources[0]);

            var secondVolume = CreateSecondClient(samplePaths.Volume, host);
            IGeoVolume second = secondVolume;
            yield return PollUntil(() => AllChunksFilled(second), "the second client's chunks to fill");
            var secondSource = ((VoxelEditStores)secondVolume.EditStores).SourceFor(key);
            var clock = region.World.Services.Clock;
            _observations = CoreKit.Observations.CreateContext("networked_sample_" + Guid.NewGuid().ToString("N"));
            _client = new RegionClient(new IReplicatedSource[] { secondSource }, region.Hub.Settings, _observations);
            _client.BeginJoin(clock.Current);
            var message = new byte[MessageBytes];
            Assert.IsTrue(region.Hub.Region.TryWriteSnapshotHeader(message, out var headerBytes, out _));
            _client.Receive(JoinMessageKind.SnapshotHeader, new ReadOnlySpan<byte>(message, 0, headerBytes),
                clock.Current);
            Assert.AreEqual(JoinState.Live, _client.State, "the second client joins on the inline snapshot");

            Assert.IsTrue(hostVolume.TryQueryVoxel(digPoint, out var before));
            Assert.IsTrue(second.TryQueryVoxel(digPoint, out var secondBefore));
            Assert.AreEqual(before, secondBefore);
            var joinedAt = region.Hub.Region.Sequence;

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(hostVolume, digPoint, DigRadius, DigStrength));
            Assert.IsTrue(hostVolume.TryQueryVoxel(digPoint, out var dug));
            Assert.AreNotEqual(before, dug, "the dig changes the host's voxel at once");
            yield return PollUntil(() => region.Hub.Region.Sequence != joinedAt, "the hub to flush the dig");

            Assert.IsTrue(region.Hub.Region.TryWriteCatchUp(_client.Sequence, message, out var catchUpBytes));
            Assert.Greater(catchUpBytes, 0);
            _client.Receive(JoinMessageKind.CatchUp, new ReadOnlySpan<byte>(message, 0, catchUpBytes), clock.Current);

            Assert.AreEqual(JoinState.Live, _client.State);
            Assert.AreEqual(region.Hub.Region.Sequence, _client.Sequence);
            Assert.IsTrue(second.TryQueryVoxel(digPoint, out var secondAfter));
            Assert.AreEqual(dug, secondAfter, "the second client reads the host's dig");
        }

        private GeoVolume CreateSecondClient(GameObject volumePrefab, GeoVolume host) {
            var geoForgeManager = Object.FindAnyObjectByType<GeoForgeManager>();
            _secondClient = new GameObject("Networked Sample Second Client");
            _secondClient.SetActive(false);
            _secondClient.transform.SetParent(geoForgeManager.transform, false);
            var instance = Object.Instantiate(volumePrefab, host.transform.position, host.transform.rotation,
                _secondClient.transform);
            var volume = instance.GetComponent<GeoVolume>();
            volume.IsPrimaryTerrain = false;
            _secondClient.SetActive(true);

            return volume;
        }

        private static GeoVolume FindSampleVolume() {
            foreach (var volume in Object.FindObjectsByType<GeoVolume>())
                if (volume.EditStores is VoxelEditStores)
                    return volume;

            return null;
        }

        private static bool AllChunksFilled(IGeoVolume volume) {
            var engine = volume.GeoVolumeEngine;

            if (engine == null)
                return false;

            var size = engine.ConfigBlob.Value.SizeInChunks;

            if (engine.ChunkDict.Count != size.x * size.y * size.z)
                return false;

            foreach (var chunk in engine.ChunkDict.Values)
                if (!chunk.VoxelsReady)
                    return false;

            return true;
        }

        private static bool TryFindSurface(IGeoVolume volume, out Vector3 point) {
            for (var height = SurfaceSearchTop; height >= SurfaceSearchBottom; height -= 1f) {
                point = new Vector3(DigColumn.x, height, DigColumn.y);

                if (!volume.TryQuerySolid(point, out var solid))
                    continue;

                if (solid)
                    return true;
            }

            point = default;

            return false;
        }

        private static bool TryFindRegion(in RegionKey key, PlayerID player, out ReplicatedRegion found) {
            foreach (var region in Object.FindObjectsByType<ReplicatedRegion>()) {
                if (region.isSpawned && region.Hub.IsConfigured && region.Key == key && region.Hub.IsObserver(player)) {
                    found = region;

                    return true;
                }
            }

            found = null;

            return false;
        }

        private static (string Scene, GameObject Volume) SamplePaths() {
#if UNITY_EDITOR
            var scenes = AssetDatabase.FindAssets(SampleSceneName + " t:Scene", new[] { "Assets/Samples" });
            var volumes = AssetDatabase.FindAssets(SampleVolumeName + " t:Prefab", new[] { "Assets/Samples" });

            if (scenes.Length == 1 && volumes.Length == 1)
                return (AssetDatabase.GUIDToAssetPath(scenes[0]),
                    AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(volumes[0])));
#endif
            return (null, null);
        }

        private static ushort FreePort() {
            using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

            return (ushort)((IPEndPoint)probe.Client.LocalEndPoint).Port;
        }

        private static IEnumerator PollUntil(Func<bool> condition, string description) {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!condition()) {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Timed out after " + TimeoutSeconds + " s waiting for " + description);

                yield return null;
            }
        }
    }
}
