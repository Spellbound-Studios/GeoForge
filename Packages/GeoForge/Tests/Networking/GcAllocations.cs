// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine.Profiling;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Spellbound.GeoForge.Networking.Tests {
    internal static class GcAllocations {
        internal static void AssertNone(TestDelegate action, int warmUps) {
            for (var warm = 0; warm < warmUps; warm++)
                action();

            var constraint = ((IResolveConstraint)Is.Not.AllocatingGCMemory()).Resolve();
            constraint.ApplyTo(action);
            Assert.That(action, constraint);
        }

        internal static int Count(Action action) {
            var recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;

            try {
                action();
            }
            finally {
                recorder.enabled = false;
            }

            return recorder.sampleBlockCount;
        }
    }
}