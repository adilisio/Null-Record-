using System.Collections.Generic;
using System.IO;
using Game.Dialogue;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class DialogueTests
    {
        private static DialogueScript Load(string file) =>
            DialogueScript.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Dialogue", file)));

        [TestCase("dace_recruit.json")]
        [TestCase("dace_banter.json")]
        [TestCase("researcher_recruit.json")]
        [TestCase("logs.json")]
        public void DialogueFile_IsConsistent(string file)
        {
            DialogueScript s = Load(file);
            Assert.IsNotEmpty(s.nodes, "no nodes parsed");
            CollectionAssert.IsEmpty(s.Validate());
            foreach (DialogueNode n in s.nodes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(n.speaker), $"'{n.id}' has no speaker");
                Assert.IsFalse(string.IsNullOrEmpty(n.text), $"'{n.id}' has no text");
            }
        }

        [Test]
        public void DaceRecruit_AcceptPath_SetsFlags()
        {
            var flags = new HashSet<string>();
            var r = new DialogueRunner(Load("dace_recruit.json"), flags.Contains, f => flags.Add(f));
            r.Start("start");
            Assert.IsTrue(flags.Contains("met_dace"), "meeting is remembered as soon as the scene starts");

            r.Advance();
            Assert.AreEqual("intro", r.Current.id);
            Assert.AreEqual(3, r.Choices.Count);
            r.Choose(0);
            Assert.AreEqual("who", r.Current.id);
            r.Advance();
            Assert.AreEqual("offer", r.Current.id);
            r.Choose(0);
            Assert.IsTrue(flags.Contains("companion_guard"));
            Assert.AreEqual("yes", r.Current.id);
            r.Advance();
            Assert.IsTrue(r.IsFinished);
        }

        [Test]
        public void DaceRecruit_DeclinePath_LeavesThemBehind()
        {
            var flags = new HashSet<string>();
            var r = new DialogueRunner(Load("dace_recruit.json"), flags.Contains, f => flags.Add(f));
            r.Start("start");
            r.Advance();
            r.Choose(2); // say nothing
            r.Advance();
            r.Choose(1); // not yet
            r.Advance();
            Assert.IsTrue(r.IsFinished);
            Assert.IsFalse(flags.Contains("companion_guard"));
        }

        [Test]
        public void Choices_FilterOnFlags()
        {
            const string json = @"{ ""id"": ""t"", ""nodes"": [
                { ""id"": ""a"", ""speaker"": ""x"", ""text"": ""?"", ""choices"": [
                    { ""text"": ""always"", ""next"": """" },
                    { ""text"": ""needs key"", ""next"": """", ""requireFlag"": ""key"" },
                    { ""text"": ""until key"", ""next"": """", ""hideIfFlag"": ""key"" } ] } ] }";
            DialogueScript s = DialogueScript.FromJson(json);
            var flags = new HashSet<string>();
            var r = new DialogueRunner(s, flags.Contains, f => flags.Add(f));

            r.Start("a");
            CollectionAssert.AreEqual(new[] { "always", "until key" }, Texts(r));

            flags.Add("key");
            r.Start("a");
            CollectionAssert.AreEqual(new[] { "always", "needs key" }, Texts(r));
        }

        [Test]
        public void Validate_ReportsBrokenLinks()
        {
            DialogueScript s = DialogueScript.FromJson(@"{ ""id"": ""t"", ""nodes"": [ { ""id"": ""a"", ""speaker"": ""x"", ""text"": ""hi"", ""next"": ""nowhere"" } ] }");
            Assert.AreEqual(1, s.Validate().Count);
        }

        private static List<string> Texts(DialogueRunner r)
        {
            var l = new List<string>();
            foreach (DialogueChoice c in r.Choices) l.Add(c.text);
            return l;
        }
    }
}
