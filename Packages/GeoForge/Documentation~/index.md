# Spellbound.GeoForge

GeoForge turns a density field into meshes: octree chunks, marching cubes with Transvoxel seams between levels of detail, edits of any shape, materials, and queries of voxels, materials and surface cases.

## Where to start

- `README.md` at the package root: what the package is and how to install it.
- `AGENTS.md`: the front door, the rules, what the package is not for and what is not built yet.
- `Samples~`: Digging A Hole, Mining Ore Veins, Orbiting Planets and the networked sample, each a scene over the shipped prefabs.
- `Runtime/Prefabs`: `GeoForgeManager.prefab` (the manager with its chunk presenter), `SimpleVolume.prefab`, `SimpleChunk.prefab` and `LeafPrefab.prefab`.

## The parts

- A `GeoVolume` creates chunks from a `VoxelVolumeConfig` and fills them from a `GeoForgeDataGenerator` asset.
- `GeoForgeManager` schedules the march jobs, completes them in batches and hands each leaf's mesh to the presenter.
- `IChunkPresenter` and `IChunkLeaf` are the seam through which a chunk is drawn; `GameObjectChunkPresenter` is the default.
- `GeoForgeCommands` edits a volume with spheres, cubes and arcs; each chunk's `IGeoEditStore` keeps the edits.
- `TryQueryVoxel`, `TryQuerySurfaceMaterial` and `TryQuerySurfaceCase` read the voxels, the material and the marching cubes cell at a world position.
- `MaterialSideTable<TData>` carries per-material data a game reads through `GeoForgeManager.GetMaterialData<TData>`, with an inspector that syncs it against the `VoxelMaterialDatabase`.
