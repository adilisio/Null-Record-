using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Real-time combat rooms: hostiles placed in the level, a room wrapper with rewards, shock grates.</summary>
    public static partial class GreyboxLevelBuilder
    {
        private static EnemyRoom EnemyRoomAt(Transform room, string name, Rect area, (EnemyKind kind, Vector2 feet)[] spawns,
            int scrap, int xp, string flag, string log, Renderer marker, GameObject[] grates, params GameObject[] hideWhenCleared)
        {
            var roomGo = new GameObject(name);
            roomGo.transform.SetParent(room, false);
            roomGo.transform.position = new Vector3(area.center.x, area.yMin, 0f);
            EnemyRoom er = roomGo.AddComponent<EnemyRoom>();

            var list = new List<Enemy>();
            for (int i = 0; i < spawns.Length; i++) list.Add(Hostile(roomGo.transform, spawns[i].kind, spawns[i].feet, i));
            er.Configure(area, list.ToArray(), scrap, xp, flag, log, marker, hideWhenCleared);

            foreach (GameObject g in grates)
            {
                if (g == null) continue;
                ShockGrate sg = g.AddComponent<ShockGrate>();
                sg.Configure(er, g.GetComponent<Renderer>().bounds.size.x, g.GetComponent<Renderer>());
            }
            return er;
        }

        private static Enemy Hostile(Transform parent, EnemyKind kind, Vector2 feet, int index)
        {
            var go = new GameObject($"Enemy_{kind}_{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(feet.x, feet.y, 0f);
            go.AddComponent<Combatant>().Configure(Team.Enemy, Enemy.HpFor(kind), kind == EnemyKind.Warden ? 0.95f : 0.7f, kind == EnemyKind.Warden ? 2f : 1.75f, 0.2f);

            var modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(go.transform, false);
            modelRoot.localRotation = Quaternion.Euler(0f, -90f, 0f); // facing the way you come in
            CharacterBody(modelRoot, kind.ToString(), new Color(0.5f, 0.16f, 0.14f), new Color(1f, 0.25f, 0.2f));

            TextMesh alert = CombatVisuals.Text(go.transform, "Alert", new Vector3(0f, kind == EnemyKind.Warden ? 2.6f : 2.3f, -0.2f), 1.1f, new Color(1f, 0.25f, 0.2f));
            alert.text = "!";
            alert.fontStyle = FontStyle.Bold;
            alert.gameObject.SetActive(false);

            Enemy e = go.AddComponent<Enemy>();
            e.Configure(kind, modelRoot, alert);
            return e;
        }

        /// <summary>The sign at a hostile room: how to attack, per device. Hidden once the room is cleared.</summary>
        private static GameObject HostileSign(Transform room, Vector3 at) =>
            Hint(Label(room, "hostiles ahead: attack [J / click]", at, new Color(1f, 0.4f, 0.35f), 0.4f), "hostiles ahead: [ATTACK]");
    }
}
