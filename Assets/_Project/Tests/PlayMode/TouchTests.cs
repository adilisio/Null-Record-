using System.Collections;
using Game.Combat;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>On-screen controls for phones: visible on touch, relabelled per context, driving a virtual gamepad.</summary>
    public class TouchTests : GreyboxTestBase
    {
        [UnityTest]
        public IEnumerator TouchControls_ShowAndFollowContext()
        {
            TouchControls touch = Object.FindAnyObjectByType<TouchControls>();
            Assert.IsNotNull(touch, "TouchControls missing (rebuild scenes)");
            TouchControls.ForceVisible = true;
            try
            {
                yield return null;
                Assert.IsTrue(touch.Visible);
                Assert.IsNotNull(Gamepad.current, "on-screen controls create a virtual gamepad");
                StringAssert.Contains("JUMP", Labels(touch));
                yield return Wait(0.2f);
                yield return Shot("50_touch_explore");

                EncounterController enc = SecurityEncounter;
                _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
                yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
                enc.Engage(_interactor);
                yield return Until(() => !enc.IsBusy, 5f, "combat starts");
                yield return null;
                StringAssert.Contains("END TURN", Labels(touch));
                StringAssert.Contains("UNIT", Labels(touch));
                yield return Shot("51_touch_combat");
            }
            finally
            {
                TouchControls.ForceVisible = false;
            }
        }

        private static string Labels(TouchControls t)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Text x in t.GetComponentsInChildren<Text>(false)) sb.Append(x.text).Append('|');
            return sb.ToString();
        }
    }
}
