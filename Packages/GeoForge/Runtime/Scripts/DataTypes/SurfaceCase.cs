// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;

namespace Spellbound.GeoForge {
    public readonly struct SurfaceCase {
        public const int CornerCount = 8;

        private const byte EmptyCaseCode = 0x00;
        private const byte SolidCaseCode = 0xFF;

        private readonly VoxelData _corner0;
        private readonly VoxelData _corner1;
        private readonly VoxelData _corner2;
        private readonly VoxelData _corner3;
        private readonly VoxelData _corner4;
        private readonly VoxelData _corner5;
        private readonly VoxelData _corner6;
        private readonly VoxelData _corner7;

        internal SurfaceCase(
            Vector3Int cellOrigin,
            byte caseCode,
            VoxelData corner0,
            VoxelData corner1,
            VoxelData corner2,
            VoxelData corner3,
            VoxelData corner4,
            VoxelData corner5,
            VoxelData corner6,
            VoxelData corner7) {
            CellOrigin = cellOrigin;
            CaseCode = caseCode;
            _corner0 = corner0;
            _corner1 = corner1;
            _corner2 = corner2;
            _corner3 = corner3;
            _corner4 = corner4;
            _corner5 = corner5;
            _corner6 = corner6;
            _corner7 = corner7;
        }

        public Vector3Int CellOrigin { get; }

        public byte CaseCode { get; }

        public bool IsEmpty => CaseCode == EmptyCaseCode;

        public bool IsSolid => CaseCode == SolidCaseCode;

        public bool HasSurface => !IsEmpty && !IsSolid;

        public VoxelData Corner(int index) {
            return index switch {
                0 => _corner0,
                1 => _corner1,
                2 => _corner2,
                3 => _corner3,
                4 => _corner4,
                5 => _corner5,
                6 => _corner6,
                7 => _corner7,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"A surface case has corners 0 to {CornerCount - 1}.")
            };
        }
    }
}
