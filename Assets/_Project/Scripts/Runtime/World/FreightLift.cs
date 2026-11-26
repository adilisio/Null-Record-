using Game.Audio;
using Game.CameraSystem;
using Game.Companions;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A freight lift between regions. Both regions live in the same scene for now, so riding it moves the player
    /// (and the companion) to the other lift and snaps the camera there.
    /// </summary>
    public class FreightLift : Interactable
    {
        [SerializeField] private Vector3 destination;
        [SerializeField] private int arrivalFacing = 1;
        [SerializeField] private string prompt = "Take the freight lift";
        [SerializeField] private string arrivalMessage = "The freight lift grinds to a stop.";

        public Vector3 Destination => destination;

        public void Configure(Vector3 dest, int facing, string promptText, string message)
        {
            destination = dest;
            arrivalFacing = facing;
            prompt = promptText;
            arrivalMessage = message;
        }

        public override string GetPrompt(PlayerInteractor player) => prompt;

        public override void Interact(PlayerInteractor player)
        {
            GameAudio.Play(SoundId.DoorOpen, transform.position + Vector3.up * 1.5f);
            PlayerMotor motor = player.Motor;
            if (motor != null)
            {
                motor.Teleport(destination);
                motor.Face(arrivalFacing);
            }
            CompanionFollower.Active?.WarpBehindPlayer();
            SideCameraRig rig = Camera.main != null ? Camera.main.GetComponent<SideCameraRig>() : null;
            if (rig != null) rig.SetTarget(player.transform);
            Game.UI.ScreenFade.FromBlack(0.45f);
            GameMessages.Show(arrivalMessage);
        }
    }
}
