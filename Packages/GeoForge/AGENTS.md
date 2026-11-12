# Spellbound.GeoForge

## Purpose

Turns a density field into meshes: octree chunks, marching cubes with Transvoxel seams, edits of any shape, materials, and queries of voxels, materials and surface cases.

## Front door

`GeoForgeKit` is the front door. `CreateDensitySource` wraps a `GeoForgeDataGenerator` asset as an `IDensitySource`, and four doors march with no manager, octree or play mode: `Generate` fills one chunk's voxels from a density source, `March` marches them at one level of detail into a `MarchOutput`, `BuildMesh` makes a `Mesh` from that output and `Bake` bakes the mesh for a collider. `VoxelEncoding` is the one place the density and material rules live (the zero rule `IsSolid`, the material encoding with its mature bit, and the null sentinel); `ChunkLayout` gives the data width, voxel index, chunk origin and chunk coordinate.

In a scene a consumer places a `GeoForgeManager` and one or more `GeoVolume`s. The manager is an instance, not a singleton: a volume names its manager in its `manager` field, or finds the one on a parent, and two managers are two worlds that share nothing.

Four seams, each with a default:
- `IDensitySource` fills a chunk's voxels; `IGeoVolume.UseDensitySource` sets it, and the default wraps the volume's generator asset.
- `IChunkInterest` picks each octree node's level of detail for the viewers handed to `IGeoVolume.SetViewers`; `DistanceChunkInterest` is the default, `ServerChunkInterest` serves a server, and `TransformChunkViewer` feeds a transform as a viewer.
- `IChunkPresenter` draws: it hands `OctreeNode` an `IChunkLeaf` per leaf with geometry and takes it back when the leaf is released; the node owns every `Mesh` and gives the leaf the mesh, the transition mesh after every rewrite, and the volume's world-to-local matrix. The default, `GameObjectChunkPresenter` in `Spellbound.GeoForge.Presentation`, pools instances of `GeoForgeManager.LeafPrefab` with a mesh filter and a mesh renderer; a server build leaves that assembly out, so nothing is drawn there and the manager says so once.
- `IChunkCollisionPresenter` collides: it hands the node an `IChunkCollisionLeaf` per leaf, which takes the same mesh and the volume's world-to-local matrix. The default, `MeshColliderChunkPresenter`, lives in the runtime assembly so a server build collides; it bakes each batch's meshes on worker threads, places its colliders in `GeoForgeManager.CollisionScene` (the manager's scene unless set), and takes the layer and physics material of the leaf prefab unless its own fields name them.

A manager takes a presenter component on its own object first, then the factory in `ChunkPresenterDefaults`; a presenter set by code wins over both, and setting either slot to null opts the manager out of drawing or of collision.

Edits go through `GeoForgeCommands` (`RemoveSphere`, `AddSphere` and the cube and arc commands) over an `IGeoVolume`; the edit store of a chunk is an `IGeoEditStore`. Queries go through one volume, or through the manager over its primary volumes: `TryQueryVoxel` answers the voxel at a world position, `TryQuerySurfaceMaterial` the material of the cell enclosing it, `TryQuerySurfaceCase` a `SurfaceCase` (the marching cubes cell with its origin, its eight corners in the march's corner order, its case code and whether it is empty, solid or crossed by the surface) and `TryQuerySolid` the zero rule there; `IsMeshReady`, `IsColliderReady` and `IsBoxReady` answer readiness under a `VolumeVersion`, and `TryCopyRunList` and `TryCopySolidRuns` copy a chunk's runs with the version they were read at. `SurfaceCases.Compute` answers surface cases over a chunk's voxel array. A volume raises `Changed` with a `VolumeChange` after an edit and `ResidencyChanged` with a `ChunkResidency`, both after the manager has finished applying a march batch, never inside it, so a subscriber may edit terrain.

<!-- spellbound:samples 34c4396d -->
Samples in `Samples~`, imported from the Package Manager's Samples tab:
- Supplimentary Assets: Additional Assets, including the Shadergraph from which the Material Shader was generated (`Samples~/SupplementaryAssets`)
- Digging A Hole: Sample Scene for digging a large and very deep hole. (`Samples~/DiggingAHole`)
- Mining Ore Veins: Sample Scene for Mining discrete ore veins where the ore feels like it has a health pool (`Samples~/MiningOreVeins`)
- Orbiting Planets: Sample Scene for Orbiting Planets; GeoForge volumes in motion. (`Samples~/OrbitingPlanets`)
- Networked Digging a Hole: Networked Version of the Digging a Hole sample scene. (`Samples~/NetworkedSample`)
<!-- /spellbound:samples -->

<!-- spellbound:depends c8e6687f -->
Depends on:
- `com.spellboundstudios.core` in [1.1.9,3.0.0)
- `com.unity.entities` 1.3.15
- `com.unity.entities.graphics` 1.3.14
- `com.unity.mathematics` 1.3.2
- `com.unity.burst` 1.8.19
- `com.unity.collections` 2.5.7
- `com.unity.modules.physics` 1.0.0
Bridges, each compiled only when its package is installed:
- `Spellbound.GeoForge.Networking` when `com.spellboundstudios.networking` is in [0.0.0,2.0.0)
<!-- /spellbound:depends -->

<!-- spellbound:doors e3bd200c -->
Bridge doors in this project, the constructors of the types a bridge assembly adds:
- `GeoForgeNetworkingKit` in `Spellbound.GeoForge.Networking`
  - `static VoxelEditStores CreateEditStores(IGeoVolume volume, ref VoxelEditSourceSettings settings, ObservationContext observations)`
<!-- /spellbound:doors -->

## Rules

The binding rules are the line text at https://github.com/Spellbound-Studios/.github/tree/main/line

- Nothing in the runtime assembly draws: a mesh reaches a renderer only through `IChunkPresenter`, and the runtime assembly never references `Spellbound.GeoForge.Presentation`.
- A query outside loaded voxel data answers false, never a default voxel.
- The case code's bit order is the march's: corner `i` of `McTablesBlobAsset.RegularCornerOffset` sets bit `i` when its density is zero or above.
- `octreePrefab` keeps its serialized name so a scene's leaf prefab survives; code reads it as `LeafPrefab`.
- The density and material rules are written only in `VoxelEncoding`; every job, producer and sample calls it.

## Not for

- A world plan or generation passes: WorldForge's.
- Sculpting, voxelising a mesh and optimisation: GeoForge Pro's.
- A demo controller or a second march: the package ships one profile, `BarycentricMaterialRenderingProfile`.
- A fixed chunk presenter: the GameObject presenter is a default a consumer replaces.

## Gaps

- The default presenters register on entering play mode (`RuntimeInitializeOnLoadMethod`), so a manager driven in edit mode has no default presenter until one is set by code or added as a component.
- The volume configuration and the marching tables are still blob assets, and the three data factories stay beside the seam that replaces them, until GeoForge Pro leaves the manifests that compile against them.
- No operation group yet: `core help` lists nothing for GeoForge, and the old console command `TerraformHelpCommand` stays until the old console leaves Core.
- No `Surface.txt` yet.
- Chunk interest over a viewer source in Core (W2a-7) is not built: Core has no viewer source at dev 70f6b4e, so a volume takes its viewers through `IGeoVolume.SetViewers`.

## Ledger
