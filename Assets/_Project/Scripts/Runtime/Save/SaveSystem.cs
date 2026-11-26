using System;
using System.IO;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// Everything needed to put the player back where they were. World objects record their state as
    /// flags (e.g. "taken_pickup_keycard_l2", "opened_door_security"), so flags carry most of the world.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string scene;
        public string savedAtUtc;
        public string reason;
        public string room;
        /// <summary>Player-facing room name for the Continue summary.</summary>
        public string roomName;

        public Vector3 position;
        public int facing = 1;
        public Vector3 checkpoint;

        public int keycardLevel;
        public int protections;
        public int scrap;
        /// <summary>Party XP (missing in older saves, so it reads as 0).</summary>
        public int xp;
        public string[] tools = Array.Empty<string>();
        public string[] flags = Array.Empty<string>();

        /// <summary>Combat roster key of the companion in the party, or empty.</summary>
        public string companion = "";
    }

    /// <summary>Reads and writes the save file. Writes go to a temp file first so a crash can't leave half a save.</summary>
    public static class SaveSystem
    {
        public const string FileName = "save_0.json";

        /// <summary>Tests point this at a scratch folder. Null = Application.persistentDataPath.</summary>
        public static string DirectoryOverride { get; set; }

        public static string Folder => string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;
        public static string FilePath => Path.Combine(Folder, FileName);
#if UNITY_WEBGL && !UNITY_EDITOR
        // Browsers: keep the save in PlayerPrefs (IndexedDB-backed and flushed reliably by PlayerPrefs.Save).
        private const string PrefsKey = "save_0";
        public static bool Exists => PlayerPrefs.HasKey(PrefsKey);

        public static void Write(SaveData data)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static SaveData Read()
        {
            if (!Exists) return null;
            SaveData d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PrefsKey));
            if (d == null || d.version <= 0)
            {
                Delete();
                return null;
            }
            d.tools ??= Array.Empty<string>();
            d.flags ??= Array.Empty<string>();
            return d;
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
#else
        public static bool Exists => File.Exists(FilePath);

        public static void Write(SaveData data)
        {
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, prettyPrint: true));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        /// <summary>The save, or null if there is none or it can't be read (a broken file is set aside as .corrupt).</summary>
        public static SaveData Read()
        {
            if (!Exists) return null;
            try
            {
                SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (d == null || d.version <= 0) throw new InvalidDataException("empty or unversioned save");
                d.tools ??= Array.Empty<string>();
                d.flags ??= Array.Empty<string>();
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Couldn't read {FilePath}: {e.Message}. Starting fresh; the file was kept as .corrupt.");
                string bad = FilePath + ".corrupt";
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(FilePath, bad);
                return null;
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
        }
#endif
    }
}
