using Game.Core;
using Game.Dialogue;
using Game.Player;
using Game.World;
using UnityEngine;

namespace Game.Companions
{
    /// <summary>
    /// A companion before recruitment: talk to them (Interact) to run their recruit conversation.
    /// When the conversation sets <see cref="recruitFlag"/>, they join and start following.
    /// </summary>
    [RequireComponent(typeof(CompanionFollower))]
    public class CompanionNPC : Interactable
    {
        [SerializeField] private string displayName = "Dace";
        [SerializeField] private TextAsset dialogue;
        [SerializeField] private string startNode = "start";
        [Tooltip("Used when talking to them again after the first time.")]
        [SerializeField] private string repeatNode = "again";
        [SerializeField] private string metFlag = "met_dace";
        [SerializeField] private string recruitFlag = "companion_guard";

        private DialogueScript _script;
        private CompanionFollower _follower;

        public bool Recruited { get; private set; }
        public string RecruitFlag => recruitFlag;

        public void Configure(string name, TextAsset json, string met, string recruit)
        {
            displayName = name;
            dialogue = json;
            metFlag = met;
            recruitFlag = recruit;
        }

        private void Awake()
        {
            _follower = GetComponent<CompanionFollower>();
            _follower.enabled = false;
            if (dialogue != null) _script = DialogueScript.FromJson(dialogue.text);
        }

        public override bool IsAvailable(PlayerInteractor player) => !Recruited && _script != null && base.IsAvailable(player);

        public override string GetPrompt(PlayerInteractor player) => $"Talk to {displayName}";

        public override void Interact(PlayerInteractor player)
        {
            PlayerInventory inv = player.Inventory;
            bool met = inv != null && inv.HasFlag(metFlag);
            DialogueUI.Ensure().Play(_script, met ? repeatNode : startNode, player, () =>
            {
                if (inv != null && inv.HasFlag(recruitFlag)) Recruit(player);
            });
        }

        /// <summary>Joins the party immediately (also used by tests and, later, save loading).</summary>
        public void Recruit(PlayerInteractor player, bool announce = true)
        {
            if (Recruited) return;
            Recruited = true;
            player.Inventory?.SetFlag(recruitFlag);
            _follower.Begin(player);
            enabled = false; // no longer an interactable
            if (announce) GameMessages.Show($"{displayName} joined you.");
        }
    }
}
