using System;
using System.Collections.Generic;
using Game.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class SynthTests
    {
        private static IEnumerable<SoundId> AllSounds() => (SoundId[])Enum.GetValues(typeof(SoundId));

        [TestCaseSource(nameof(AllSounds))]
        public void Sound_IsAudibleCleanAndInRange(SoundId id)
        {
            for (int v = 0; v < Synth.Variants(id); v++)
            {
                float[] s = Synth.Generate(id, v);
                Assert.Greater(s.Length, Synth.Rate / 50, "too short");
                float peak = 0f;
                double sumSq = 0;
                foreach (float x in s)
                {
                    Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), "NaN/inf sample");
                    peak = Mathf.Max(peak, Mathf.Abs(x));
                    sumSq += x * x;
                }
                Assert.LessOrEqual(peak, 1f, "clips");
                Assert.Greater(peak, 0.3f, "nearly silent");
                Assert.Greater(Math.Sqrt(sumSq / s.Length), 0.005, "no energy");
            }
        }

        [TestCaseSource(nameof(AllSounds))]
        public void Loops_JoinWithoutAClick(SoundId id)
        {
            if (!Synth.IsLoop(id)) Assert.Pass("not a loop");
            float[] s = Synth.Generate(id, 0);
            Assert.Less(Mathf.Abs(s[s.Length - 1] - s[0]), 0.15f, "jump at the loop point");
            Assert.GreaterOrEqual(s.Length, Synth.Rate * 2, "loops should be at least 2 s");
        }

        [Test]
        public void Generation_IsDeterministic()
        {
            CollectionAssert.AreEqual(Synth.Generate(SoundId.Footstep, 2), Synth.Generate(SoundId.Footstep, 2));
            CollectionAssert.AreNotEqual(Synth.Generate(SoundId.Footstep, 0), Synth.Generate(SoundId.Footstep, 1), "variants differ");
        }
    }
}
