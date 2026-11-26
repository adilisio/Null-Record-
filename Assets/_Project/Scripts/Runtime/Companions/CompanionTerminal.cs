using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.World;
using UnityEngine;

namespace Game.Companions
{
    /// <summary>
    /// Where companions who aren't in the party wait (one companion is active at a time, per the GDD), and where
    /// the player swaps which one comes along. Benched companions stand in a row beside it.
    /// </summary>
    public class CompanionTerminal : Interactable
    {
        public static CompanionTerminal Instance { get; private set; }

        [SerializeField] private Vector3 benchOffset = new Vector3(1.4f, 0f, 0.6f);
        [SerializeField] private float benchSpacing = 0.9f;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Recruited companions in a stable order (by object name).</summary>
        public static List<CompanionNPC> RecruitedCompanions()
        {
            var list = new List<CompanionNPC>();
            foreach (CompanionNPC n in FindObjectsByType<CompanionNPC>(FindObjectsSortMode.None))
            {
                if (n.Recruited) list.Add(n);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        /// <summary>Where a benched companion stands: beside the terminal, or where they are if there's no terminal.</summary>
        public static Vector3 BenchSpotFor(CompanionFollower follower)
        {
            if (Instance == null) return follower.transform.position;
            int slot = 0;
            foreach (CompanionNPC n in RecruitedCompanions())
            {
                if (n.GetComponent<CompanionFollower>() == follower) break;
                slot++;
            }
            return Instance.transform.position + Instance.benchOffset + Vector3.right * (Instance.benchSpacing * slot);
        }

        public override bool IsAvailable(PlayerInteractor player) => base.IsAvailable(player) && RecruitedCompanions().Count >= 2;

        public override string GetPrompt(PlayerInteractor player)
        {
            CompanionNPC current = CompanionFollower.Active != null ? CompanionFollower.Active.GetComponent<CompanionNPC>() : null;
            return current != null ? $"Switch companion (with you: {current.DisplayName})" : "Call a companion";
        }

        public override void Interact(PlayerInteractor player)
        {
            List<CompanionNPC> all = RecruitedCompanions();
            if (all.Count == 0) return;
            int index = -1;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].GetComponent<CompanionFollower>() == CompanionFollower.Active) index = i;
            }
            CompanionNPC next = all[(index + 1) % all.Count];
            CompanionFollower follower = next.GetComponent<CompanionFollower>();
            if (follower == CompanionFollower.Active) return;
            follower.Begin(player);
            follower.WarpBehindPlayer();
            GameMessages.Show($"{next.DisplayName} is with you now.");
        }
    }
}
