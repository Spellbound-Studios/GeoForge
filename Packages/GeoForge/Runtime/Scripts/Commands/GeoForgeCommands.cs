// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class GeoForgeCommands {
        private static readonly uint4 AllMaterialsMask = new(uint.MaxValue);

        #region Cube

        /// <summary>
        ///     Removes ("digs into") a uniform cube, no falloff, hard edges - halfExtent is in
        ///     voxels, not world units. delta is the magnitude to subtract (always applied as
        ///     negative internally). Returns false if the action was rejected.
        /// </summary>
        public static bool RemoveCube(IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, int delta) {
            return RemoveCube(geoVolume, worldPosition, halfExtent, delta, AllMaterialsMask);
        }

        public static bool RemoveCube(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, int delta, uint4 allowedMaterialsMask) {
            return TerraformCubeCommand.Execute(
                geoVolume, worldPosition, halfExtent, (short)-delta, 0, allowedMaterialsMask);
        }

        /// <summary>
        ///     Adds ("fills") a uniform cube, no falloff, hard edges - halfExtent is in voxels, not
        ///     world units. Returns false if the action was rejected.
        /// </summary>
        public static bool AddCube(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, short delta, byte material) {
            return AddCube(geoVolume, worldPosition, halfExtent, delta, material, AllMaterialsMask);
        }

        public static bool AddCube(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, short delta, byte material,
            uint4 allowedMaterialsMask) {
            return TerraformCubeCommand.Execute(
                geoVolume, worldPosition, halfExtent, delta, material, allowedMaterialsMask);
        }

        #endregion

        #region Sphere

        /// <summary>
        ///     Removes ("digs into") a spherical region with smooth falloff. Returns false if the
        ///     action was rejected.
        /// </summary>
        public static bool RemoveSphere(IGeoVolume geoVolume, Vector3 worldPosition, float radius, int delta) {
            return RemoveSphere(geoVolume, worldPosition, radius, delta, VoxelData.NullSentinelValue, AllMaterialsMask);
        }

        public static bool RemoveSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, int delta, uint4 allowedMaterialsMask) {
            return TerraformSphereCommand.Execute(
                geoVolume, worldPosition, radius, (short)-delta, VoxelData.NullSentinelValue, allowedMaterialsMask);
        }

        public static bool RemoveSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, int delta, byte materialIndex,
            uint4 allowedMaterialsMask) {
            return TerraformSphereCommand.Execute(
                geoVolume, worldPosition, radius, (short)-delta, materialIndex, allowedMaterialsMask);
        }

        /// <summary>
        ///     Adds ("deposits onto") a spherical region with smooth falloff. Returns false if the
        ///     action was rejected.
        /// </summary>
        public static bool AddSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, short delta, byte material) {
            return AddSphere(geoVolume, worldPosition, radius, delta, material, AllMaterialsMask);
        }

        public static bool AddSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, short delta, byte material,
            uint4 allowedMaterialsMask) {
            return TerraformSphereCommand.Execute(
                geoVolume, worldPosition, radius, delta, material, allowedMaterialsMask);
        }

        #endregion

        #region Arc

        /// <summary>
        ///     Removes ("digs into") a half-disc "arc" region - see TerraformArcCommand's own doc
        ///     comment for the shape's exact geometry. No delta parameter: an arc always subtracts a
        ///     fixed maximum, same reasoning as the original TerraformCommands.TerraformArc. Returns false
        ///     if rejected.
        /// </summary>
        public static bool RemoveArc(
            IGeoVolume geoVolume, Vector3 worldPosition, Vector3 direction, Vector3 upHint, float radius,
            float thickness) {
            return RemoveArc(geoVolume, worldPosition, direction, upHint, radius, thickness, AllMaterialsMask);
        }

        public static bool RemoveArc(
            IGeoVolume geoVolume, Vector3 worldPosition, Vector3 direction, Vector3 upHint, float radius,
            float thickness, uint4 allowedMaterialsMask) {
            return TerraformArcCommand.Execute(
                geoVolume, worldPosition, direction, upHint, radius, thickness, allowedMaterialsMask);
        }

        #endregion
    }
}