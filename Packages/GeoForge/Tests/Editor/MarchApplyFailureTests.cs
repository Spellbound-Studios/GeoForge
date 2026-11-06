// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spellbound.GeoForge.Tests {
    public class MarchApplyFailureTests {
        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void ALeafThatThrowsWhilePreparedDoesNotStopTheOtherResults() {
            _world = new PlaneWorld(0f, Vector3Int.zero, Vector3Int.right);
            var presenter = new AcquireThrowingChunkPresenter();
            _world.Manager.Presenter = presenter;
            _world.Manager.CollisionPresenter = null;
            LogAssert.Expect(LogType.Error, new Regex(nameof(AcquireThrowingChunkPresenter.ThrowsOnNextAcquire)));

            Assert.DoesNotThrow(() => _world.ValidateEveryChunk(Vector3.zero));

            Assert.IsFalse(_world.Manager.IsApplyingMarchResults);
            Assert.AreEqual(1, presenter.Recording.Acquired.Count);
            Assert.AreEqual(0, PendingCount("_pendingMarchJobData"));
            Assert.AreEqual(0, PendingCount("_pendingTransitionMarchJobData"));
        }

        private int PendingCount(string fieldName) {
            var pending = typeof(GeoForgeManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(pending, fieldName);

            return ((ICollection)pending.GetValue(_world.Manager)).Count;
        }
    }
}
