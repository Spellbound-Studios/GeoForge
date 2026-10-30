# Spellbound.GeoForge

## Purpose

Turns a density field into meshes: octree chunks, marching cubes with Transvoxel seams, edits of any shape, materials, and queries of voxels, materials and surface cases.

## Front door

`GeoForgeKit` is the front door and holds nothing yet: the density source seam and the public voxel encoding arrive behind it with the world model's density stories. Today a consumer places a `GeoForgeManager` and a `GeoVolume` in a scene, hands the volume a `VoxelVolumeConfig` and a `GeoForgeDataGenerator` asset, and the manager marches the chunks the volume creates.

Drawing goes through one seam: `GeoForgeManager.Presenter` is an `IChunkPresenter`, which hands `OctreeNode` an `IChunkLeaf` per leaf with geometry and takes it back when the leaf is released; the node owns every `Mesh` and gives the leaf the mesh, the transition mesh, the volume's world-to-local matrix and its visibility. The default, `GameObjectChunkPresenter` in `Spellbound.GeoForge.Presentation`, pools instances of `GeoForgeManager.LeafPrefab` with a mesh filter, a mesh collider and a mesh renderer, and sits on the shipped `GeoForgeManager.prefab`; a server build leaves that assembly out, so nothing is drawn there and the manager says so once.

Edits go through `GeoForgeCommands` (`RemoveSphere`, `AddSphere` and the cube and arc commands) over an `IGeoVolume`; the edit store of a chunk is an `IGeoEditStore`. Queries go through the manager over the primary volumes or through one volume: `TryQueryVoxel` answers the voxel at a world position, `TryQuerySurfaceMaterial` the material of the cell enclosing it, and `TryQuerySurfaceCase` a `SurfaceCase`, the marching cubes cell at that position with its origin, its eight corners in the march's corner order, its case code and whether it is empty, solid or crossed by the surface. `SurfaceCases.Compute` answers the same over a chunk's voxel array.

<!-- spellbound:samples 34c4396d -->
Samples in `Samples~`, imported from the Package Manager's Samples tab:
- Supplimentary Assets: Additional Assets, including the Shadergraph from which the Material Shader was generated (`Samples~/SupplementaryAssets`)
- Digging A Hole: Sample Scene for digging a large and very deep hole. (`Samples~/DiggingAHole`)
- Mining Ore Veins: Sample Scene for Mining discrete ore veins where the ore feels like it has a health pool (`Samples~/MiningOreVeins`)
- Orbiting Planets: Sample Scene for Orbiting Planets; GeoForge volumes in motion. (`Samples~/OrbitingPlanets`)
- Networked Digging a Hole: Networked Version of the Digging a Hole sample scene. (`Samples~/NetworkedSample`)
<!-- /spellbound:samples -->

<!-- spellbound:depends 061e43c1 -->
Depends on:
- `com.spellboundstudios.core` in [1.1.9,3.0.0)
- `com.unity.entities` 1.3.15
- `com.unity.entities.graphics` 1.3.14
- `com.unity.mathematics` 1.3.2
- `com.unity.burst` 1.8.19
- `com.unity.collections` 2.5.7
<!-- /spellbound:depends -->

## Rules

The binding rules are the line text at https://github.com/Spellbound-Studios/.github/tree/main/line

- Nothing in the runtime assembly draws: a mesh reaches a renderer only through `IChunkPresenter`, and the runtime assembly never references `Spellbound.GeoForge.Presentation`.
- A query outside loaded voxel data answers false, never a default voxel.
- The case code's bit order is the march's: corner `i` of `McTablesBlobAsset.RegularCornerOffset` sets bit `i` when its density is zero or above.
- `octreePrefab` keeps its serialized name so a scene's leaf prefab survives; code reads it as `LeafPrefab`.

## Not for

- A world plan or generation passes: WorldForge's.
- Sculpting, voxelising a mesh and optimisation: GeoForge Pro's.
- A demo controller or a second march: the package ships one profile, `BarycentricMaterialRenderingProfile`.
- A fixed chunk presenter: the GameObject presenter is a default a consumer replaces.

## Gaps

- The density source seam, the public voxel encoding, point queries under a version, the run-list read, the change and residency event and the chunk interest over Core's viewer source are not built yet; the level of detail target is still a transform the volume resolves, and `GeoForgeManager` is still a singleton the scene holds.
- The presenter is one part: a server build has no presenter and so no terrain collider until the collision part that compiles in every build exists.
- The volume configuration and the marching tables are still blob assets, and the three data factories stay beside the seam that replaces them, until GeoForge Pro leaves the manifests that compile against them.
- No operation group yet: `core help` lists nothing for GeoForge, and the old console command `TerraformHelpCommand` stays until the old console leaves Core.
- No `Surface.txt` yet.

## Ledger
