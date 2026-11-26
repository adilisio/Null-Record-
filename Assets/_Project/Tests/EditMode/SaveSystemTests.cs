using System.IO;
using Game.Save;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class SaveSystemTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Application.temporaryCachePath, "NullRecordSaveSystemTests");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            SaveSystem.DirectoryOverride = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void RoundTrip_KeepsEverything()
        {
            var d = new SaveData
            {
                scene = "Assets/x.unity",
                position = new Vector3(1.5f, 4f, 0f),
                facing = -1,
                checkpoint = new Vector3(21f, 0f, 0f),
                keycardLevel = 2,
                protections = 1,
                scrap = 30,
                xp = 75,
                tools = new[] { "spoofer" },
                flags = new[] { "companion_guard", "taken_pickup_keycard_l2" },
                companion = "Guard",
            };
            SaveSystem.Write(d);
            Assert.IsTrue(SaveSystem.Exists);

            SaveData r = SaveSystem.Read();
            Assert.AreEqual(d.scene, r.scene);
            Assert.AreEqual(d.position, r.position);
            Assert.AreEqual(-1, r.facing);
            Assert.AreEqual(d.checkpoint, r.checkpoint);
            Assert.AreEqual(2, r.keycardLevel);
            Assert.AreEqual(1, r.protections);
            Assert.AreEqual(30, r.scrap);
            Assert.AreEqual(75, r.xp);
            CollectionAssert.AreEqual(d.tools, r.tools);
            CollectionAssert.AreEqual(d.flags, r.flags);
            Assert.AreEqual("Guard", r.companion);
            Assert.AreEqual(SaveData.CurrentVersion, r.version);
        }

        [Test]
        public void Overwrite_LeavesNoTempFile()
        {
            SaveSystem.Write(new SaveData { scrap = 1 });
            SaveSystem.Write(new SaveData { scrap = 2 });
            Assert.AreEqual(2, SaveSystem.Read().scrap);
            Assert.IsFalse(File.Exists(SaveSystem.FilePath + ".tmp"));
        }

        [Test]
        public void CorruptFile_IsSetAsideAndTreatedAsNoSave()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(SaveSystem.FilePath, "{ this is not json");
            LogAssertIgnore();
            Assert.IsNull(SaveSystem.Read());
            Assert.IsFalse(SaveSystem.Exists);
            Assert.IsTrue(File.Exists(SaveSystem.FilePath + ".corrupt"));
        }

        [Test]
        public void NoFile_ReadsNull()
        {
            Assert.IsNull(SaveSystem.Read());
        }

        [Test]
        public void Delete_RemovesTheSave()
        {
            SaveSystem.Write(new SaveData());
            SaveSystem.Delete();
            Assert.IsFalse(SaveSystem.Exists);
        }

        private static void LogAssertIgnore() => UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
    }
}
