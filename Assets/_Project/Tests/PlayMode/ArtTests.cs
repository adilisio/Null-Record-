using System.Collections;
using Game.Audio;
using Game.Companions;
using Game.Dialogue;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Third-party art is wired in: animated character models, recorded sounds and set dressing.</summary>
    public class ArtTests : GreyboxTestBase
    {
        [UnityTest]
        public IEnumerator Player_IsAnAnimatedModel_ThatRuns()
        {
            CharacterAnimator ca = _motor.GetComponentInChildren<CharacterAnimator>();
            Assert.IsNotNull(ca, "player body is a character prefab");
            Assert.IsNotNull(ca.Animator, "has an Animator");
            Assert.IsNotNull(ca.Animator.runtimeAnimatorController, "Animator has the character controller");
            Assert.IsFalse(ca.Animator.applyRootMotion, "root motion would fight the motor");
            SkinnedMeshRenderer skin = _motor.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin, "skinned mesh");
            Assert.IsNotNull(skin.sharedMaterial.GetTexture("_BaseMap"), "skin texture assigned");

            float x0 = X;
            Press(Key.D);
            yield return Wait(0.5f); // short of the jump crate, so still running
            Assert.Greater(X, x0 + 1f, "player is moving");
            Assert.Greater(ca.Speed, 2f, "the animator sees the player moving");
            Assert.IsTrue(ca.Animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), "playing Run while moving");
            Release(Key.D);
            yield return Wait(0.8f);
            Assert.IsTrue(ca.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "back to Idle when stopped");
        }

        [UnityTest]
        public IEnumerator Dace_AndHostiles_UseCharacterModels()
        {
            yield return null;
            CompanionNPC dace = Object.FindAnyObjectByType<CompanionNPC>();
            Assert.IsNotNull(dace.GetComponentInChildren<SkinnedMeshRenderer>(), "Dace has a model");

            int hostiles = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null || !t.name.StartsWith("Hostile_")) continue;
                hostiles++;
                Assert.IsNotNull(t.GetComponentInChildren<SkinnedMeshRenderer>(), $"{t.name} has a model");
                Assert.IsNotNull(t.GetComponentInChildren<CharacterAnimator>(), $"{t.name} animates");
            }
            Assert.AreEqual(3, hostiles, "three idle hostiles in Security");
        }

        [UnityTest]
        public IEnumerator RecordedSounds_ReplaceTheSynthWhereMapped()
        {
            yield return null;
            SoundId[] fourVariants = { SoundId.Footstep, SoundId.CombatStep, SoundId.LadderClank, SoundId.Hit };
            foreach (SoundId id in fourVariants)
            {
                for (int v = 0; v < 4; v++) Assert.IsTrue(AudioManager.IsRecorded(id, v), $"{id} variant {v} is recorded");
            }
            SoundId[] single = { SoundId.Hurt, SoundId.Knockback, SoundId.UnitDown, SoundId.DoorOpen, SoundId.Zap, SoundId.Land };
            foreach (SoundId id in single) Assert.IsTrue(AudioManager.IsRecorded(id), $"{id} is recorded");
            Assert.IsFalse(AudioManager.IsRecorded(SoundId.MusicExplore), "music stays synthesised");
            Assert.IsNotNull(AudioManager.Clip(SoundId.Footstep, 2), "library holds the recorded clip");
        }

        [UnityTest]
        public IEnumerator Dace_HasARenderedPortrait()
        {
            yield return null;
            Sprite face = Speakers.Portrait("dace");
            Assert.IsNotNull(face, "Resources/Portraits/dace (rebuild scenes)");
            Assert.AreEqual(256, face.texture.width);
            Assert.IsNull(Speakers.Portrait("pa"), "the PA has no face; it keeps the colour block");
        }

        [UnityTest]
        public IEnumerator Level_IsDressed_WithoutColliders()
        {
            yield return null;
            GameObject dressing = GameObject.Find("Dressing");
            Assert.IsNotNull(dressing, "set dressing group (rebuild scenes)");
            Assert.Greater(dressing.GetComponentsInChildren<Renderer>().Length, 150, "panels and props");
            Assert.AreEqual(0, dressing.GetComponentsInChildren<Collider>().Length, "dressing must not change collision");
        }
    }
}
