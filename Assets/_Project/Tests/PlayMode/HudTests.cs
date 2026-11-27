using System.Collections;
using System.Reflection;
using Game.Combat;
using Game.World;
using Game.Dialogue;
using Game.Player;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Screen-space HUDs, device-aware hints and touch input.</summary>
    public class HudTests : GreyboxTestBase
    {
        [UnityTest]
        public IEnumerator TouchTap_IsNotBoundToAttack()
        {
            yield return null;
            var field = typeof(PlayerInputReader).GetField("actions", BindingFlags.NonPublic | BindingFlags.Instance);
            var asset = (InputActionAsset)field.GetValue(_motor.GetComponent<PlayerInputReader>());
            if (asset == null) asset = InputSystem.actions;
            Assert.IsNotNull(asset, "player input actions");
            foreach (InputAction a in asset)
            {
                foreach (InputBinding b in a.bindings)
                {
                    Assert.IsFalse(b.path.Contains("primaryTouch/tap"), $"{a.name} is bound to a touchscreen tap, which double-fires with the on-screen buttons");
                }
            }
        }

        [UnityTest]
        public IEnumerator Hints_FollowTheDevice()
        {
            GreyboxHUD hud = Object.FindAnyObjectByType<GreyboxHUD>();
            Assert.IsNotNull(hud, "exploration HUD");
            LogTerminal log = GameObject.Find("Log_holding_shift").GetComponent<LogTerminal>();
            _motor.Teleport(log.transform.position + new Vector3(0f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == log, 2f, "log prompt");
            yield return null;
            StringAssert.StartsWith("E:", hud.PromptText, "keyboard prompt");
            StringAssert.StartsWith("HP 5/5", hud.HpText, "HP shows");
            HintLabel sign = null;
            foreach (HintLabel h in Object.FindObjectsByType<HintLabel>(FindObjectsSortMode.None))
            {
                if (h.GetComponent<TextMesh>().text.Contains("hostiles") && Mathf.Abs(h.transform.position.x - 46f) < 1f) sign = h;
            }
            Assert.IsNotNull(sign, "the Security sign follows the device");
            StringAssert.Contains("[J / click]", sign.GetComponent<TextMesh>().text);
            yield return Shot("30_hud_prompt");

            TouchControls.ForceVisible = true;
            try
            {
                yield return null;
                yield return null;
                StringAssert.StartsWith("USE:", hud.PromptText, "touch prompt");
                StringAssert.Contains("[ATTACK]", sign.GetComponent<TextMesh>().text);
            }
            finally
            {
                TouchControls.ForceVisible = false;
            }
            yield return null;
            yield return null;
            StringAssert.Contains("[J / click]", sign.GetComponent<TextMesh>().text, "keyboard wording again");
        }

        [UnityTest]
        public IEnumerator DialogueTap_AdvancesExactlyOneLine()
        {
            Touchscreen ts = InputSystem.AddDevice<Touchscreen>();
            try
            {
                ts.MakeCurrent();
                _motor.Teleport(new Vector3(38.3f, 0.05f, 0f)); // clear of the keycard pickup at x 36
                yield return Until(() => _interactor.Current == DaceNpc, 2f, "talk prompt");
                DaceNpc.Interact(_interactor);
                yield return Until(() => DialogueUI.Instance != null && DialogueUI.Instance.IsOpen, 1f, "dialogue opens");
                yield return Wait(0.2f);
                string first = DialogueUI.Instance.CurrentLine;

                Vector2 middle = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
                InputSystem.QueueStateEvent(ts, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = middle });
                yield return null;
                InputSystem.QueueStateEvent(ts, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = middle });
                yield return Wait(0.2f);
                string second = DialogueUI.Instance.CurrentLine;
                Assert.AreNotEqual(first, second, "a tap off the buttons advances");
                yield return Wait(0.3f);
                Assert.AreEqual(second, DialogueUI.Instance.CurrentLine, "and only once");
            }
            finally
            {
                InputSystem.RemoveDevice(ts);
                TouchControls.ResetTouchSeen();
            }
        }
    }
}
