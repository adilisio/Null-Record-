using System.Collections;
using Game.Audio;
using Game.Combat;
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
            CompanionNPC dace = DaceNpc;
            Assert.IsNotNull(dace.GetComponentInChildren<SkinnedMeshRenderer>(), "Dace has a model");

            int hostiles = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null || !t.name.StartsWith("Hostile_")) continue;
                hostiles++;
                Assert.IsNotNull(t.GetComponentInChildren<SkinnedMeshRenderer>(), $"{t.name} has a model");
                Assert.IsNotNull(t.GetComponentInChildren<CharacterAnimator>(), $"{t.name} animates");
            }
            Assert.AreEqual(9, hostiles, "three idle hostiles each in Security, the Server Hall and the Specimen Hall");
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
        public IEnumerator Crouch_PlaysAPose_InsteadOfSquashingTheModel()
        {
            CharacterAnimator ca = _motor.GetComponentInChildren<CharacterAnimator>();
            Vector3 scale = ca.transform.localScale;
            Press(Key.C);
            yield return Wait(0.5f);
            Assert.IsTrue(_motor.IsCrouching, "crouching");
            Assert.IsTrue(ca.Animator.GetCurrentAnimatorStateInfo(0).IsName("Crouch"), "crouch pose");
            Assert.AreEqual(scale, ca.transform.localScale, "the model isn't squashed");
            yield return Shot("17_crouch");
            Release(Key.C);
            yield return Wait(0.6f);
            AnimatorStateInfo st = ca.Animator.GetCurrentAnimatorStateInfo(0);
            string state = st.IsName("Idle") ? "Idle" : st.IsName("Run") ? "Run" : st.IsName("Jump") ? "Jump" : st.IsName("Crouch") ? "Crouch" : "?";
            Assert.IsFalse(_motor.IsCrouching, "motor stands up");
            Assert.AreEqual("Idle", state, $"stands back up (crouch param {ca.Animator.GetBool(CharacterAnimator.CrouchId)}, transition {ca.Animator.IsInTransition(0)})");
        }

        [UnityTest]
        public IEnumerator RecordedSounds_AreLoudnessMatched()
        {
            yield return null;
            Assert.IsNotNull(Resources.Load<TextAsset>("Sounds/gains"), "gains table (rebuild scenes)");
            bool anyAdjusted = false;
            for (int v = 0; v < 4; v++)
            {
                float g = AudioManager.GainOf(AudioManager.Clip(SoundId.Footstep, v));
                Assert.That(g, Is.InRange(0.25f, 4f));
                anyAdjusted |= Mathf.Abs(g - 1f) > 0.01f;
            }
            Assert.IsTrue(anyAdjusted, "footsteps were matched to the synth's level");
            Assert.AreEqual(1f, AudioManager.GainOf(AudioManager.Clip(SoundId.MusicExplore)), "synth clips are untouched");
        }

        [UnityTest]
        public IEnumerator Greybox_IsCoveredByKitMeshes_ButKeepsItsColliders()
        {
            yield return null;
            GameObject floor = GameObject.Find("Shell/Floor");
            Assert.IsFalse(floor.GetComponent<Renderer>().enabled, "greybox floor hidden");
            Assert.IsTrue(floor.GetComponent<Collider>().enabled, "but still solid");
            foreach (string name in new[] { "Catwalk", "Loft", "Mezzanine", "Platform_High" })
            {
                GameObject box = GameObject.Find(name);
                Assert.IsNotNull(box, name);
                Assert.IsFalse(box.GetComponent<Renderer>().enabled, $"{name} renders as a kit mesh");
                Assert.IsNotNull(GameObject.Find(name + "_Model"), $"{name}_Model");
            }
            Assert.IsNotNull(GameObject.Find("Door_L2/Panel/Panel_Model"), "the door mesh rides on the sliding panel");
            foreach (string block in new[] { "Block_LedgeClimb", "Block_Archive", "Raised_Block" })
            {
                Assert.IsTrue(GameObject.Find(block).GetComponent<Renderer>().enabled, $"{block} stays a solid box");
                Assert.IsNotNull(GameObject.Find(block + "_Panels"), $"{block} is faced with kit panels");
            }
        }

        [UnityTest]
        public IEnumerator Combat_PlaysAttackClips_AndTheTargetFlinches()
        {
            EncounterController enc = SecurityEncounter;
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
            enc.Engage(_interactor);
            yield return Until(() => !enc.IsBusy, 5f, "opening");
            CombatState st = enc.State;
            Unit me = enc.Selected;
            // Put an enemy right next to the Subject so Strike is in reach.
            Unit foe = null;
            foreach (Unit e in st.Alive(Team.Enemy)) { foe = e; break; }
            foreach (Tile t in st.Grid.Tiles)
            {
                if (t.Col == me.Tile.Col + 1 && Mathf.Abs(t.Y - me.Tile.Y) < 0.3f && !st.Occupied(t)) { foe.Tile = t; break; }
            }
            ((UnitView)foe.View).Root.position = foe.Tile.World;
            foe.Hp = 50; // survive the hit, so the flinch shows
            yield return null;

            CharacterAnimator mine = ((UnitView)me.View).Root.GetComponentInChildren<CharacterAnimator>();
            CharacterAnimator theirs = ((UnitView)foe.View).Root.GetComponentInChildren<CharacterAnimator>();
            Assert.IsTrue(st.TryUseSkill(me, 0, foe.Tile), "Strike");
            yield return Until(() => mine.Animator.GetCurrentAnimatorStateInfo(0).IsName("Strike"), 1f, "Subject plays Strike");
            yield return Wait(0.15f);
            yield return Shot("18_attack");
            yield return Until(() => theirs.LastAction == "Hit", 2f, "target flinches");
            yield return Until(() => !enc.IsBusy, 3f, "events done");
            yield return Wait(0.8f);
            Assert.IsTrue(mine.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "back to Idle after the clip");
            UnitView fv2 = (UnitView)foe.View;
            Assert.AreEqual(foe.Hp, fv2.DisplayHp, "the shown HP has caught up with the real HP");
            Assert.IsNotNull(fv2.Root.Find("CombatLabels/HpFill"), "HP bar over the head");
        }

        [UnityTest]
        public IEnumerator Filmstrip_BatonSwing_ThenAKnockout()
        {
            EncounterController enc = SecurityEncounter;
            DaceNpc.Recruit(_interactor);
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
            enc.Engage(_interactor);
            yield return Until(() => !enc.IsBusy, 5f, "opening");
            CombatState st = enc.State;
            Unit dace = null, foe = null;
            foreach (Unit u in st.Alive(Team.Player)) if (!u.IsLeader) dace = u;
            foreach (Unit e in st.Alive(Team.Enemy)) { foe = e; break; }
            Assert.IsNotNull(dace, "Dace is in the fight");
            Tile spot = null;
            foreach (Tile t in st.Grid.Tiles)
            {
                if (t.Col == dace.Tile.Col - 1 && Mathf.Abs(t.Y - dace.Tile.Y) < 0.3f) spot = t;
            }
            Assert.IsNotNull(spot, "room for the foe left of Dace");
            foe.Tile = spot;
            UnitView fv = (UnitView)foe.View;
            fv.Root.position = spot.World;
            fv.SetFacing(1);
            foe.Hp = 50;
            yield return Wait(0.3f);

            // Baton swing at full speed: a frame every ~60 ms.
            Assert.IsTrue(st.TryUseSkill(dace, 0, foe.Tile), "Baton");
            for (int i = 0; i < 9; i++)
            {
                yield return Shot($"19_swing_{i}");
                yield return Wait(0.05f);
            }
            yield return Until(() => !enc.IsBusy, 3f, "swing done");

            // Knockout: one more hit finishes it.
            foe.Hp = 1;
            Unit me = enc.Selected;
            CharacterAnimator theirs = fv.Root.GetComponentInChildren<CharacterAnimator>();
            Transform foot = fv.Root;
            float maxTilt = 0f;
            dace.Acted = false; // a second action this turn, for the test only
            Assert.IsTrue(st.TryUseSkill(dace, 0, foe.Tile), "finishing Baton");
            for (int i = 0; i < 12; i++)
            {
                yield return Shot($"20_down_{i}");
                if (foot != null && foot.gameObject.activeInHierarchy) maxTilt = Mathf.Max(maxTilt, Vector3.Angle(foot.up, Vector3.up));
                yield return Wait(0.09f);
            }
            Assert.AreEqual("Down", theirs.LastAction, "knockout clip");
            Assert.Greater(maxTilt, 60f, "the body tips over");
            Assert.IsFalse(fv.Root.Find("CombatLabels").gameObject.activeSelf, "labels hidden while it falls");
            yield return Until(() => !enc.IsBusy, 4f, "knockout done");
            Assert.IsFalse(fv.Root.gameObject.activeSelf, "and is cleared away");
        }

        [UnityTest]
        public IEnumerator Prefabs_CarryTheirWeapons()
        {
            yield return null;
            foreach ((string who, string name) in new[] { ("Dace", "Weapon_Dace") })
            {
                CompanionNPC dace = DaceNpc;
                Assert.IsNotNull(dace.transform.Find("Body/Model/Root/HipsCtrl/Hips/Spine/Chest/UpperChest/RightShoulder/RightArm/RightForeArm/RightHand/" + name), $"{who} holds {name}");
            }
            int armed = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("Weapon_") && t.parent != null && t.parent.name == "RightHand") armed++;
            }
            Assert.GreaterOrEqual(armed, 11, "Dace, the Researcher and nine idle hostiles hold something (the Subject is unarmed)");
        }

        [UnityTest]
        public IEnumerator Cover_GivesNoHeightBonus()
        {
            EncounterController enc = SecurityEncounter;
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
            enc.Engage(_interactor);
            yield return Until(() => !enc.IsBusy, 5f, "opening");
            Tile coverTop = null;
            foreach (Tile t in enc.State.Grid.Tiles)
            {
                if (Mathf.Abs(t.World.x - 47.5f) < 0.3f && t.Y > 0.5f) coverTop = t;
            }
            Assert.IsNotNull(coverTop, "the crate top is still a tile (the lane has no way around it)");
            Assert.IsTrue(coverTop.CoverTop, "marked as cover");
            Unit foe = null;
            foreach (Unit e in enc.State.Alive(Team.Enemy)) { foe = e; break; }
            Tile below = enc.State.Grid.Find(coverTop.Col + 1, 0f);
            Assert.AreEqual(foe.Def.Skills[0].Damage, enc.State.PreviewDamage(coverTop, foe.Def.Skills[0], below, null), "no height bonus from the crate");
            Assert.IsNotNull(enc.State.Grid.Find(5, 2.15f) ?? enc.State.Grid.Find(9, 2.15f), "platforms are still tiles");
        }

        [UnityTest]
        public IEnumerator Companion_GoesDown_ThenGetsUpAfterTheFight()
        {
            EncounterController enc = SecurityEncounter;
            DaceNpc.Recruit(_interactor);
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
            enc.Engage(_interactor);
            yield return Until(() => !enc.IsBusy, 5f, "opening");
            CombatState st = enc.State;
            Unit dace = null;
            foreach (Unit u in st.Alive(Team.Player)) if (!u.IsLeader) dace = u;
            Transform body = ((UnitView)dace.View).Root;

            // Walk Dace onto the arc grate at 1 HP.
            Tile grate = null;
            foreach (Tile t in st.Grid.Tiles) if (t.Hazard > 0) grate = t;
            Tile next = null;
            foreach (Tile t in st.Grid.Tiles)
            {
                if (t.Col == grate.Col + 1 && Mathf.Abs(t.Y - grate.Y) < 0.3f && !st.Occupied(t)) next = t;
            }
            dace.Tile = next;
            body.position = next.World;
            dace.Hp = 1;
            Assert.IsTrue(st.TryMove(dace, grate), "step onto the grate");
            yield return Until(() => !enc.IsBusy, 4f, "Dace goes down");
            Assert.IsFalse(dace.Alive);
            Assert.Greater(Vector3.Angle(body.up, Vector3.up), 60f, "Dace is lying down");
            Assert.AreEqual(Phase.Player, st.Phase, "losing the companion doesn't end the fight");
            yield return Shot("26_companion_down");

            // Lose the fight; Dace gets up when it resets.
            foreach (Unit u in st.Units) if (u.IsLeader) u.Hp = 1;
            for (int i = 0; i < 6 && !st.IsOver; i++)
            {
                st.EndPlayerTurn();
                yield return Until(() => !enc.IsBusy, 10f, "enemy phase");
            }
            yield return Until(() => enc.Status == EncounterStatus.Idle, 4f, "defeat resets");
            Assert.Less(Vector3.Angle(body.up, Vector3.up), 5f, "Dace is upright again");
            Assert.AreEqual("", body.GetComponentInChildren<CharacterAnimator>().LastAction, "out of the Down pose");
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
