using System;
using System.IO;
using Game.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Writes every synthesised placeholder sound to Screenshots/audio/*.wav for listening outside Unity.</summary>
    public static class SoundExport
    {
        [MenuItem("Null Record/Export Placeholder Sounds", priority = 31)]
        public static void ExportAll()
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots", "audio");
            Directory.CreateDirectory(dir);
            int n = 0;
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                WriteWav(Path.Combine(dir, $"{id}.wav"), Synth.Generate(id, 0), Synth.Rate);
                n++;
            }
            Debug.Log($"[Null Record] Exported {n} sounds to {dir}");
        }

        /// <summary>16-bit mono PCM WAV.</summary>
        public static void WriteWav(string path, float[] samples, int rate)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1); // PCM
            w.Write((short)1); // mono
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (float s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
