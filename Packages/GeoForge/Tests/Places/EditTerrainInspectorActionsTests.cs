// Copyright (c) Spellbound Studios. All rights reserved.

using System.Linq;
using NUnit.Framework;
using Spellbound.Places;
using Spellbound.Places.Editor;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class EditTerrainInspectorActionsTests {
        private PlaceDefinition _place;

        [SetUp]
        public void CreatePlace() {
            _place = ScriptableObject.CreateInstance<PlaceDefinition>();
            _place.AddLayer(new VoxelLayer { LayerName = "voxels" });
            _place.AddLayer(new PlacementPointsTestLayer { LayerName = "deposits" });
        }

        [TearDown]
        public void DestroyPlace() {
            Object.DestroyImmediate(_place);
        }

        [Test]
        public void TheRegistryListsEditTerrainForAVoxelLayer() {
            var labels = PlaceLayerInspectorActionRegistry.Default.ActionsFor(_place, 0).Select(action => action.Label);

            Assert.That(labels, Has.Exactly(1).EqualTo("Edit terrain"));
        }

        [Test]
        public void TheRegistryListsNoEditTerrainForAnotherLayer() {
            var labels = PlaceLayerInspectorActionRegistry.Default.ActionsFor(_place, 1).Select(action => action.Label);

            Assert.That(labels, Has.No.Member("Edit terrain"));
        }

        [Test]
        public void TheActionsAnswerTheVoxelLayerTypeAndADefaultVoxelSize() {
            var actions = new EditTerrainInspectorActions();

            Assert.That(actions.LayerType, Is.EqualTo(typeof(VoxelLayer)));
            Assert.That(actions.VoxelSize, Is.EqualTo(EditTerrainSession.DefaultVoxelSize));
            Assert.That(EditTerrainSession.DefaultVoxelSize, Is.EqualTo(1f));
        }
    }
}
