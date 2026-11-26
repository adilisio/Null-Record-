using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// A conversation loaded from JSON (see Assets/_Project/Dialogue). Nodes are lines; a node either
    /// continues to <see cref="DialogueNode.next"/>, offers choices, or ends the conversation.
    /// </summary>
    [Serializable]
    public class DialogueScript
    {
        public string id;
        public DialogueNode[] nodes = Array.Empty<DialogueNode>();

        private Dictionary<string, DialogueNode> _index;

        public DialogueNode Get(string nodeId)
        {
            if (_index == null)
            {
                _index = new Dictionary<string, DialogueNode>();
                foreach (DialogueNode n in nodes) _index[n.id] = n;
            }
            return nodeId != null && _index.TryGetValue(nodeId, out DialogueNode node) ? node : null;
        }

        public static DialogueScript FromJson(string json)
        {
            DialogueScript s = JsonUtility.FromJson<DialogueScript>(json);
            if (s == null) throw new ArgumentException("Dialogue JSON could not be parsed.");
            s.nodes ??= Array.Empty<DialogueNode>();
            foreach (DialogueNode n in s.nodes)
            {
                n.choices ??= Array.Empty<DialogueChoice>();
                n.setFlags ??= Array.Empty<string>();
            }
            return s;
        }

        /// <summary>Problems such as links to missing nodes. Empty when the script is consistent.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (DialogueNode n in nodes)
            {
                if (string.IsNullOrEmpty(n.id)) problems.Add("node without id");
                else if (!seen.Add(n.id)) problems.Add($"duplicate node id '{n.id}'");
            }
            foreach (DialogueNode n in nodes)
            {
                if (!string.IsNullOrEmpty(n.next) && Get(n.next) == null) problems.Add($"'{n.id}' -> missing '{n.next}'");
                foreach (DialogueChoice c in n.choices)
                {
                    if (!string.IsNullOrEmpty(c.next) && Get(c.next) == null) problems.Add($"'{n.id}' choice '{c.text}' -> missing '{c.next}'");
                }
            }
            return problems;
        }
    }

    [Serializable]
    public class DialogueNode
    {
        public string id;
        /// <summary>Speaker key (see <see cref="Speakers"/>).</summary>
        public string speaker;
        [TextArea] public string text;
        /// <summary>Following node when there are no choices; empty = end.</summary>
        public string next;
        public DialogueChoice[] choices = Array.Empty<DialogueChoice>();
        /// <summary>Flags set when this line is shown.</summary>
        public string[] setFlags = Array.Empty<string>();
    }

    [Serializable]
    public class DialogueChoice
    {
        public string text;
        public string next;
        public string[] setFlags = Array.Empty<string>();
        /// <summary>Only offered when this flag is set.</summary>
        public string requireFlag;
        /// <summary>Hidden once this flag is set.</summary>
        public string hideIfFlag;
    }

    /// <summary>
    /// Display info per speaker key. A portrait sprite at Resources/Portraits/&lt;key&gt; (rendered from the
    /// character model by the editor tools) is used when present; otherwise the colour and an initial.
    /// </summary>
    public static class Speakers
    {
        public struct Info
        {
            public string Name;
            public Color Color;
        }

        public static Sprite Portrait(string key) => string.IsNullOrEmpty(key) ? null : Resources.Load<Sprite>($"Portraits/{key}");

        public static Info Get(string key)
        {
            switch (key)
            {
                case "dace": return new Info { Name = "Dace", Color = new Color(0.32f, 0.42f, 0.55f) };
                case "researcher": return new Info { Name = "Researcher", Color = new Color(0.3f, 0.75f, 0.65f) };
                case "pa": return new Info { Name = "Site PA", Color = new Color(0.55f, 0.2f, 0.18f) };
                case "log_staff": return new Info { Name = "Staff log", Color = new Color(0.25f, 0.45f, 0.7f) };
                case "log_subject": return new Info { Name = "Subject note", Color = new Color(0.7f, 0.5f, 0.25f) };
                default: return new Info { Name = string.IsNullOrEmpty(key) ? "" : key, Color = new Color(0.3f, 0.3f, 0.32f) };
            }
        }
    }

    /// <summary>Steps through a <see cref="DialogueScript"/>. Plain C#; flags come from the caller.</summary>
    public sealed class DialogueRunner
    {
        private readonly DialogueScript _script;
        private readonly Func<string, bool> _hasFlag;
        private readonly Action<string> _setFlag;
        private readonly List<DialogueChoice> _visible = new List<DialogueChoice>();

        public DialogueNode Current { get; private set; }
        public bool IsFinished => Current == null;
        /// <summary>Choices offered on the current node (after flag filtering).</summary>
        public IReadOnlyList<DialogueChoice> Choices => _visible;

        public DialogueRunner(DialogueScript script, Func<string, bool> hasFlag, Action<string> setFlag)
        {
            _script = script;
            _hasFlag = hasFlag ?? (_ => false);
            _setFlag = setFlag ?? (_ => { });
        }

        public void Start(string nodeId) => Enter(nodeId);

        /// <summary>Continue past a line without choices.</summary>
        public void Advance()
        {
            if (Current == null || _visible.Count > 0) return;
            Enter(Current.next);
        }

        public void Choose(int index)
        {
            if (Current == null || index < 0 || index >= _visible.Count) return;
            DialogueChoice c = _visible[index];
            foreach (string f in c.setFlags)
            {
                if (!string.IsNullOrEmpty(f)) _setFlag(f);
            }
            Enter(c.next);
        }

        private void Enter(string nodeId)
        {
            Current = string.IsNullOrEmpty(nodeId) ? null : _script.Get(nodeId);
            _visible.Clear();
            if (Current == null) return;
            foreach (string f in Current.setFlags)
            {
                if (!string.IsNullOrEmpty(f)) _setFlag(f);
            }
            foreach (DialogueChoice c in Current.choices)
            {
                if (!string.IsNullOrEmpty(c.requireFlag) && !_hasFlag(c.requireFlag)) continue;
                if (!string.IsNullOrEmpty(c.hideIfFlag) && _hasFlag(c.hideIfFlag)) continue;
                _visible.Add(c);
            }
        }
    }
}
