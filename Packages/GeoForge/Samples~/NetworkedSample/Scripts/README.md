# Networked Digging a Hole

Two players dig one GeoForge volume. Each player's dig shows at once on the digger's machine, and the host flushes it to every other player in the region.

## What the sample needs

- Spellbound Networking (`com.spellboundstudios.networking`) and PurrNet (`dev.purrnet.purrnet`) in the project. Without them the sample's assembly is compiled out.
- The Input System for the controller, as in the other GeoForge samples.

## How it works

- `NetworkedVolume` is a `GeoVolume`. Before its chunks are made it asks `GeoForgeNetworkingKit.CreateEditStores` for the volume's `VoxelEditStores` and hands them to the volume through `UseEditStores`, so every chunk's edits land in a voxel edit source per region.
- On start it creates the world's networking with `NetworkingPurrNetKit.CreateWorldNetworking`, from `Assets/VoxelEditReplication.asset`, and disposes it when the network manager stops as server or client, or when the volume is destroyed, whichever comes first.
- `VoxelRegionBinder` binds the region's `VoxelEditSource` with the layer's policy when a region opens, and lets it go when the region closes.
- `PlayerViewpoint` is the player prefab PurrNet's `PlayerSpawner` spawns. Its owner moves it with the camera, and on the host it feeds the world's viewer registry, so the regions around each player open for that player.
- `NetworkedChunk` is a plain chunk. It holds no store and no network identity; the volume gives it the store.
- `VoxelEditRegion.prefab` carries a `ReplicatedRegion` with the Networking package's `RegionVisibility` rule set, and is listed in `Prefabs/NetworkPrefabs.asset` beside the player prefab.
- The network manager uses `Visibility/SampleNetworkRules.asset`, PurrNet's default rules, so ownership and server checks stay on.

## Settings

`Assets/VoxelEditReplication.asset` holds one layer, `voxel edits`:

- write policy `Owner`: each player's dig is applied on their own machine first, and the host relays it;
- `cellSize` 128 metres, one chunk of the sample volume;
- `enterRadius` 128 and `exitRadius` 192 metres;
- the region prefab `Prefabs/VoxelEditRegion.prefab`;
- the event backstop place `Both`.

Every other field keeps the Networking package's default.

## Transport

The network manager uses PurrNet's UDP transport on 127.0.0.1, port 5000: two processes on one machine. The editor starts as the host; a Multiplayer Play Mode clone, or a client build, starts as a client and connects to it.

## Running it

1. Import the sample and open `NetworkedGeoForge.unity`.
2. Enter play mode in the editor: the editor starts as the host.
3. Start a Multiplayer Play Mode clone, or a client build: it joins the host as the second player.
4. Move with W, A, S and D and the mouse. Press 1 to dig and 2 to add material where you look. The other player sees the change.
