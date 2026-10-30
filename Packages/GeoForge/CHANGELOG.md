## [Unreleased]

* The material side table inspector left the runtime folder for GeoForge's first editor assembly, Spellbound.GeoForge.Editor, and is drawn in UI Toolkit: the table's own fields, then a Material Database card whose Sync and Validate buttons show the result in the inspector as well as on the console. The two Spellbound/GeoForge menu items moved with it. MaterialSideTableEditor and MaterialSideTableUtility are removed. The editor assembly needs a Spellbound.Core that holds the shared editor controls.
* The first test assembly, Spellbound.GeoForge.Editor.Tests, with the inspector's edit-mode tests.
* Drawing a chunk goes through a seam, IChunkPresenter and IChunkLeaf. OctreeNode still creates, fills and destroys every mesh and hands it to the leaf it acquired from GeoForgeManager.Presenter, which the manager takes from a component on its own GameObject when nothing set it. Without a presenter the terrain still marches, records its empty addresses and answers queries, but nothing is drawn and the manager warns once. The leaf pool left GeoForgeManager (GetPooledObject, ReleasePooledObject and ClearPool are removed); octreePrefab keeps its serialized name and is reached through GeoForgeManager.LeafPrefab.
* The default presenter, GameObjectChunkPresenter, is in a new assembly, Spellbound.GeoForge.Presentation, which server builds (UNITY_SERVER) leave out. It pools leaf GameObjects made from LeafPrefab under OctreeLeafPool as the manager did, and the GeoForgeManager prefab carries it. The first play-mode test assembly, Spellbound.GeoForge.Tests, builds the sample volume from the package prefabs.
* SimpleVolume.prefab stored its values under field names GeoVolume no longer reads, so the volume found no chunk prefab and switched itself off. The same values are stored under the current names.
* SurfaceCase and SurfaceCases.Compute describe the marching cubes cell at a voxel position: its origin, its eight corners in the march's corner order and its case code, with IsEmpty, IsSolid and HasSurface. GeoForgeManager.TryQuerySurfaceCase, IGeoVolume.TryQuerySurfaceCase and GeoVolumeEngine.TryQuerySurfaceCase find the cell enclosing a world position the same way TryQuerySurfaceMaterial does.

* A GeoForgeManager outside the shipped prefab takes the default presenter by itself: the presentation assembly registers GameObjectChunkPresenter as the default when the player loads, and the manager adds it to its own GameObject the first time a leaf needs drawing, so a scene that holds its own manager draws as before. A manager that was given no presenter marches, keeps its empty addresses and answers queries, warns once through Core's logger, and draws every held leaf once a presenter arrives.
* Removed: BasicWasdController (the demo controller), OldMarchingCubeJob, OldTransitionMarchingCubeJob, OldMaterialRenderingProfile and the OldMaterialRenderingProfile asset; a project whose rendering profile asset used the old script takes BarycentricProfile. The material table inspector takes the editor look and shows In sync as passed.
* The package declares the line's floor, Unity 6000.3, and no longer lists Core as a registry dependency: every assembly that references Core is constrained on Core's define, with a Bootstrap assembly that offers the install when Core is missing. GeoForgeKit is the front door, Identity.asset and AGENTS.md carry the package's identity and generated blocks, and Documentation~/index.md is the documentation index.

## [0.6.24] - 2026-10-08

* The march jobs, the blob assets and the sparse voxel data are public instead of internal.

## [0.5.0] - 2026-07-21

* Improved fidelity of voxel edits/deltas.
* Added a barycentric coordinate shader for rendering the intersection between different materials more accurately
* Provided a scriptableobject type to swap in different profiles for how to handle rendering, and variations on the fundamental marching cubes algorithms.

## [0.4.0] - 2026-07-10

* Revised MaterialDatabase to include fallback Colors/Sliders, and alternate textures/fallbacks.
* Revised VoxelData to contain a bit for if the voxel is "mature" or not.
* Added Shader Capability to render alternate textures/fallbacks as moss or snow or discoloration, but only for mature voxels.

## [0.3.0] - 2026-07-02

* Added Per-Material FlatShading Option

## [0.2.3] - 2026-06-16

* Terrain edits by "Delta" rather than simple replacement
* Improved compatibility with saving/loading
* Updated per Spellbound.Core 1.0.x

## [0.1.0] - 2026-02-27

* Included Samples
