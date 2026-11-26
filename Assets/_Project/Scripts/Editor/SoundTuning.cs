using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Loudness-matches the recorded clips in Resources/Sounds to the synthesised sounds they replace:
    /// for each recorded clip, gain = RMS(synth) / RMS(recording), clamped. The mix levels in AudioManager were
    /// tuned against the synth, so this keeps the balance. Results go to Resources/Sounds/gains.json, which the
    /// AudioManager reads at runtime (WebGL can't analyse compressed clips in the browser). Run by Build All Scenes.
    /// </summary>
    public static class SoundTuning
    {
        private const string Dir = "Assets/_Project/Resources/Sounds";

        [MenuItem("Null Record/Tune Recorded Sound Levels", priority = 6)]
        public static void Run()
        {
            var lines = new List<string>();
            foreach (SoundId id in System.Enum.GetValues(typeof(SoundId)))
            {
                for (int v = 0; v < Synth.Variants(id); v++)
                {
                    string path = $"{Dir}/{id}_{v}.ogg";
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) continue;
                    AudioImporterSampleSettings ss = importer.defaultSampleSettings;
                    if (ss.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        ss.loadType = AudioClipLoadType.DecompressOnLoad;
                        importer.defaultSampleSettings = ss;
                        importer.SaveAndReimport();
                    }
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    float rec = Rms(clip);
                    float syn = Rms(Synth.Generate(id, v));
                    float gain = rec > 1e-5f ? Mathf.Clamp(syn / rec, 0.25f, 4f) : 1f;
                    lines.Add($"    {{ \"key\": \"{id}_{v}\", \"gain\": {gain.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} }}");
                }
            }
            var sb = new StringBuilder("{ \"entries\": [\n");
            sb.Append(string.Join(",\n", lines));
            sb.Append("\n] }\n");
            File.WriteAllText($"{Dir}/gains.json", sb.ToString());
            AssetDatabase.ImportAsset($"{Dir}/gains.json");
            Debug.Log($"[Null Record] Tuned {lines.Count} recorded sounds.");
        }

        private static float Rms(AudioClip clip)
        {
            if (clip == null) return 0f;
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return 0f;
            return Rms(data);
        }

        /// <summary>RMS over the loud part (samples above 10% of peak), so silence padding doesn't skew it.</summary>
        private static float Rms(float[] data)
        {
            float peak = 0f;
            foreach (float x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
            if (peak <= 0f) return 0f;
            double sum = 0;
            int n = 0;
            foreach (float x in data)
            {
                if (Mathf.Abs(x) < peak * 0.1f) continue;
                sum += x * x;
                n++;
            }
            return n == 0 ? 0f : Mathf.Sqrt((float)(sum / n));
        }
    }
}
