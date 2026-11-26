using System;
using UnityEngine;

namespace Game.Audio
{
    public enum SoundId
    {
        // Player
        Footstep,
        Jump,
        Land,
        LadderClank,
        Hurt,
        // World
        DoorOpen,
        DoorDenied,
        Pickup,
        SteamLoop,
        // UI and dialogue
        UiMove,
        UiConfirm,
        DialogueBlip,
        Save,
        // Combat
        CombatStep,
        Swing,
        Zap,
        Hit,
        Knockback,
        UnitDown,
        PhasePlayer,
        PhaseEnemy,
        Victory,
        Defeat,
        // Loops
        AmbHolding,
        AmbMaintenance,
        AmbSecurity,
        CombatPulse,
    }

    /// <summary>
    /// Placeholder sound design, synthesised in code (no audio files): filtered noise, oscillators and
    /// envelopes tuned for an industrial, percussive palette. Deterministic for a given (id, variant).
    /// Swap in recorded assets later by mapping the same <see cref="SoundId"/>s to clips.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;

        public static bool IsLoop(SoundId id) =>
            id == SoundId.SteamLoop || id == SoundId.AmbHolding || id == SoundId.AmbMaintenance || id == SoundId.AmbSecurity || id == SoundId.CombatPulse;

        /// <summary>How many variants are generated (played at random to avoid repetition).</summary>
        public static int Variants(SoundId id) => id == SoundId.Footstep || id == SoundId.LadderClank || id == SoundId.CombatStep || id == SoundId.Hit ? 4 : 1;

        public static AudioClip CreateClip(SoundId id, int variant)
        {
            float[] data = Generate(id, variant);
            AudioClip clip = AudioClip.Create($"{id}_{variant}", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static float[] Generate(SoundId id, int variant)
        {
            var rng = new System.Random(((int)id + 1) * 7919 + variant * 104729);
            float[] s = id switch
            {
                SoundId.Footstep => Footstep(rng, variant, 1f),
                SoundId.CombatStep => Footstep(rng, variant, 0.6f),
                SoundId.Jump => Jump(rng),
                SoundId.Land => Land(rng),
                SoundId.LadderClank => Clank(rng, 480f + variant * 55f, 0.32f),
                SoundId.Hurt => Hurt(rng),
                SoundId.DoorOpen => DoorOpen(rng),
                SoundId.DoorDenied => Denied(),
                SoundId.Pickup => Pickup(),
                SoundId.SteamLoop => SteamLoop(rng),
                SoundId.UiMove => Blip(1700f, 0.035f, 0.25f, false),
                SoundId.UiConfirm => TwoTone(900f, 1350f, 0.045f, 0.35f),
                SoundId.DialogueBlip => Blip(420f, 0.05f, 0.22f, true),
                SoundId.Save => TwoTone(520f, 780f, 0.07f, 0.18f),
                SoundId.Swing => Swing(rng),
                SoundId.Zap => Zap(rng),
                SoundId.Hit => HitSound(rng, variant),
                SoundId.Knockback => Knock(rng),
                SoundId.UnitDown => UnitDown(rng),
                SoundId.PhasePlayer => PhasePlayer(),
                SoundId.PhaseEnemy => PhaseEnemy(rng),
                SoundId.Victory => Arpeggio(new[] { 392f, 523f, 659f, 784f }, 0.13f),
                SoundId.Defeat => Arpeggio(new[] { 392f, 330f, 262f, 196f }, 0.18f),
                SoundId.AmbHolding => AmbHolding(rng),
                SoundId.AmbMaintenance => AmbMaintenance(rng),
                SoundId.AmbSecurity => AmbSecurity(rng),
                SoundId.CombatPulse => CombatPulse(rng),
                _ => new float[Rate / 10],
            };
            Sanitize(s, IsLoop(id) ? 0.7f : 0.9f);
            return s;
        }

        // ---------------------------------------------------------------- one-shots

        private static float[] Footstep(System.Random rng, int v, float gain)
        {
            float[] s = Buf(0.14f);
            float[] n = Noise(rng, s.Length);
            Lowpass(n, 900f + v * 220f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float thump = Mathf.Sin(Phase(t, 75f, 42f, 0.06f)) * Env(t, 0.002f, 0.045f);
                s[i] = (n[i] * Env(t, 0.001f, 0.035f) * 1.4f + thump * 0.8f) * gain;
            }
            return s;
        }

        private static float[] Jump(System.Random rng)
        {
            float[] s = Buf(0.2f);
            float[] n = Noise(rng, s.Length);
            SweepLowpass(n, 500f, 2600f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * Env(T(i), 0.03f, 0.09f) * 0.55f;
            return s;
        }

        private static float[] Land(System.Random rng)
        {
            float[] s = Buf(0.3f);
            float[] n = Noise(rng, s.Length);
            Lowpass(n, 700f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                s[i] = n[i] * Env(t, 0.001f, 0.07f) * 1.6f + Mathf.Sin(Phase(t, 60f, 32f, 0.1f)) * Env(t, 0.002f, 0.11f);
            }
            return s;
        }

        /// <summary>Struck metal: inharmonic partials with fast-decaying highs.</summary>
        private static float[] Clank(System.Random rng, float f0, float length)
        {
            float[] s = Buf(length);
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
            float[] decays = { 0.22f, 0.11f, 0.05f, 0.025f };
            float[] amps = { 0.5f, 0.35f, 0.25f, 0.15f };
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float v = 0f;
                for (int k = 0; k < ratios.Length; k++) v += Mathf.Sin(2f * Mathf.PI * f0 * ratios[k] * t) * amps[k] * Mathf.Exp(-t / decays[k]);
                if (t < 0.004f) v += (float)(rng.NextDouble() * 2 - 1) * (1f - t / 0.004f) * 0.6f;
                s[i] = v;
            }
            return s;
        }

        private static float[] Hurt(System.Random rng)
        {
            float[] s = Buf(0.45f);
            float[] n = Noise(rng, s.Length);
            Lowpass(n, 1800f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float v = Mathf.Sin(Phase(t, 110f, 38f, 0.2f)) * Env(t, 0.002f, 0.18f) + n[i] * Env(t, 0.001f, 0.12f) * 0.9f;
                s[i] = (float)Math.Tanh(v * 2.2f) * 0.8f;
            }
            return s;
        }

        /// <summary>Pneumatic hiss, a motor whir that rises as the panel lifts, and a clunk at the top.</summary>
        private static float[] DoorOpen(System.Random rng)
        {
            float[] s = Buf(1f);
            float[] hiss = Noise(rng, s.Length);
            Highpass(hiss, 2500f);
            float[] whir = new float[s.Length];
            float ph = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float f = Mathf.Lerp(85f, 150f, Mathf.Clamp01(t / 0.7f));
                ph += f / Rate;
                whir[i] = (ph % 1f) * 2f - 1f; // saw
            }
            Lowpass(whir, 600f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float motor = whir[i] * Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.78f - t) / 0.08f) * 0.6f;
                float clunkT = t - 0.76f;
                float clunk = clunkT > 0f ? Mathf.Sin(Phase(clunkT, 90f, 45f, 0.08f)) * Env(clunkT, 0.001f, 0.08f) : 0f;
                s[i] = hiss[i] * Env(t, 0.01f, 0.25f) * 0.7f + motor + clunk;
            }
            return s;
        }

        private static float[] Denied()
        {
            float[] s = Buf(0.42f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                bool on = t < 0.13f || (t > 0.19f && t < 0.32f);
                s[i] = on ? Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 196f * t)) * 0.35f : 0f;
            }
            Lowpass(s, 1400f);
            return s;
        }

        private static float[] Pickup()
        {
            float[] s = Buf(0.6f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float a = Mathf.Sin(2f * Mathf.PI * 660f * t) * Env(t, 0.003f, 0.18f);
                float t2 = t - 0.1f;
                float b = t2 > 0f ? (Mathf.Sin(2f * Mathf.PI * 990f * t2) + 0.3f * Mathf.Sin(2f * Mathf.PI * 1980f * t2)) * Env(t2, 0.003f, 0.28f) : 0f;
                s[i] = (a + b) * 0.5f;
            }
            return s;
        }

        private static float[] Blip(float f, float length, float gain, bool square)
        {
            float[] s = Buf(length);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float w = Mathf.Sin(2f * Mathf.PI * f * t);
                if (square) w = Mathf.Sign(w) * 0.6f;
                s[i] = w * Env(t, 0.002f, length * 0.4f) * gain;
            }
            if (square) Lowpass(s, 2200f);
            return s;
        }

        private static float[] TwoTone(float f1, float f2, float each, float gain)
        {
            float[] s = Buf(each * 2.4f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float f = t < each ? f1 : f2;
                float lt = t < each ? t : t - each;
                s[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Env(lt, 0.002f, each * 0.6f) * gain;
            }
            return s;
        }

        private static float[] Swing(System.Random rng)
        {
            float[] s = Buf(0.18f);
            float[] n = Noise(rng, s.Length);
            SweepLowpass(n, 3200f, 600f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * Env(T(i), 0.02f, 0.07f) * 0.9f;
            return s;
        }

        /// <summary>Taser: a falling saw with crackle.</summary>
        private static float[] Zap(System.Random rng)
        {
            float[] s = Buf(0.28f);
            float ph = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float f = 1900f * Mathf.Pow(300f / 1900f, Mathf.Clamp01(t / 0.25f));
                ph += f / Rate;
                float saw = (ph % 1f) * 2f - 1f;
                float crackle = rng.NextDouble() < 0.02 ? (float)(rng.NextDouble() * 2 - 1) : 0f;
                s[i] = (saw * 0.45f + crackle * 0.8f) * Env(t, 0.002f, 0.12f);
            }
            return s;
        }

        private static float[] HitSound(System.Random rng, int v)
        {
            float[] s = Buf(0.26f);
            float[] n = Noise(rng, s.Length);
            Highpass(n, 1800f + v * 300f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float thud = Mathf.Sin(Phase(t, 130f - v * 8f, 48f, 0.07f)) * Env(t, 0.001f, 0.09f);
                s[i] = (float)Math.Tanh((thud + n[i] * Env(t, 0.0005f, 0.025f) * 1.3f) * 1.8f) * 0.85f;
            }
            return s;
        }

        private static float[] Knock(System.Random rng)
        {
            float[] whoosh = Swing(rng);
            float[] s = Buf(0.36f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float w = i < whoosh.Length ? whoosh[i] : 0f;
                float tt = t - 0.15f;
                float thud = tt > 0f ? Mathf.Sin(Phase(tt, 80f, 40f, 0.08f)) * Env(tt, 0.002f, 0.1f) : 0f;
                s[i] = w * 0.7f + thud;
            }
            return s;
        }

        private static float[] UnitDown(System.Random rng)
        {
            float[] s = Buf(0.7f);
            float[] n = Noise(rng, s.Length);
            Lowpass(n, 900f);
            float ph = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float f = Mathf.Lerp(260f, 55f, Mathf.Clamp01(t / 0.6f));
                ph += f / Rate;
                float saw = (ph % 1f) * 2f - 1f;
                s[i] = saw * Env(t, 0.005f, 0.3f) * 0.4f + n[i] * Env(t, 0.002f, 0.2f) * 0.6f;
            }
            Lowpass(s, 1500f);
            return s;
        }

        private static float[] PhasePlayer()
        {
            float[] a = Clank(new System.Random(11), 660f, 0.5f);
            float[] b = Clank(new System.Random(12), 880f, 0.5f);
            float[] s = Buf(0.65f);
            int off = (int)(0.12f * Rate);
            for (int i = 0; i < s.Length; i++) s[i] = (i < a.Length ? a[i] : 0f) * 0.6f + (i >= off && i - off < b.Length ? b[i - off] : 0f) * 0.6f;
            return s;
        }

        private static float[] PhaseEnemy(System.Random rng)
        {
            float[] s = Buf(0.8f);
            float[] ring = Clank(rng, 180f, 0.8f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float drum = Mathf.Sin(Phase(t, 70f, 38f, 0.2f)) * Env(t, 0.002f, 0.25f);
                s[i] = drum + ring[i] * 0.5f;
            }
            return s;
        }

        private static float[] Arpeggio(float[] notes, float step)
        {
            float[] s = Buf(step * notes.Length + 0.5f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < s.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float tri = Mathf.PingPong(notes[n] * t * 2f, 1f) * 2f - 1f;
                    s[i] += tri * Env(t, 0.004f, 0.35f) * 0.35f;
                }
            }
            return s;
        }

        // ---------------------------------------------------------------- loops

        private static float[] SteamLoop(System.Random rng)
        {
            float[] s = Buf(3f + Fade);
            float[] hi = Noise(rng, s.Length);
            Highpass(hi, 1600f);
            float[] lo = Noise(rng, s.Length);
            Lowpass(lo, 350f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float flutter = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                s[i] = (hi[i] * 0.8f + lo[i] * 0.9f) * flutter;
            }
            return MakeSeamless(s);
        }

        /// <summary>Fluorescent hum: 120 Hz mains and harmonics, with a faint airy bed.</summary>
        private static float[] AmbHolding(System.Random rng)
        {
            float[] s = Buf(8f + Fade);
            float[] air = Noise(rng, s.Length);
            Lowpass(air, 280f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.22f + Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.1f;
                float wobble = 0.9f + 0.1f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                s[i] = hum * wobble * 0.35f + air[i] * 0.8f;
            }
            return MakeSeamless(s);
        }

        /// <summary>Machine room: low rumble, a 1 Hz pump throb, and two distant clanks.</summary>
        private static float[] AmbMaintenance(System.Random rng)
        {
            float[] s = Buf(8f + Fade);
            float[] rumble = Brown(rng, s.Length);
            Lowpass(rumble, 180f);
            float[] c1 = Clank(new System.Random(31), 310f, 0.6f);
            float[] c2 = Clank(new System.Random(32), 260f, 0.6f);
            Lowpass(c1, 900f);
            Lowpass(c2, 900f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float beatT = t % 1f;
                float throb = Mathf.Sin(Phase(beatT, 55f, 40f, 0.1f)) * Env(beatT, 0.01f, 0.18f) * 0.35f;
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                float clanks = Sample(c1, i - (int)(2.7f * Rate)) * 0.25f + Sample(c2, i - (int)(6.1f * Rate)) * 0.2f;
                s[i] = rumble[i] * swell * 1.2f + throb + clanks;
            }
            return MakeSeamless(s);
        }

        /// <summary>Security: low drone with a soft alarm beep every two seconds.</summary>
        private static float[] AmbSecurity(System.Random rng)
        {
            float[] s = Buf(8f + Fade);
            float ph = 0f;
            float[] drone = new float[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                ph += 55f / Rate;
                drone[i] = (ph % 1f) * 2f - 1f;
            }
            Lowpass(drone, 160f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float bt = t % 2f;
                float beep = bt < 0.09f ? Mathf.Sin(2f * Mathf.PI * 880f * t) * Env(bt, 0.005f, 0.06f) * 0.18f : 0f;
                float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.08f;
                s[i] = drone[i] * 0.6f + beep + hum;
            }
            return MakeSeamless(s);
        }

        /// <summary>Combat bed at 120 BPM: kick on the beat, metallic hat on the off-beat, a clang every bar.</summary>
        private static float[] CombatPulse(System.Random rng)
        {
            const float beat = 0.5f;
            float[] s = Buf(8f);
            float[] hatNoise = Noise(rng, (int)(0.06f * Rate));
            Highpass(hatNoise, 6000f);
            float[] clang = Clank(new System.Random(41), 200f, 0.9f);
            Lowpass(clang, 1200f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float bt = t % beat;
                float kick = Mathf.Sin(Phase(bt, 95f, 42f, 0.08f)) * Env(bt, 0.002f, 0.13f) * 0.8f;
                float ot = (t + beat * 0.5f) % beat;
                float hat = ot < 0.06f ? hatNoise[(int)(ot * Rate)] * Env(ot, 0.0005f, 0.02f) * 0.5f : 0f;
                float barT = t % (beat * 4f);
                float cl = Sample(clang, (int)(barT * Rate)) * 0.35f;
                s[i] = kick + hat + cl;
            }
            return s;
        }

        // ---------------------------------------------------------------- DSP helpers

        private const float Fade = 0.5f;

        private static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];
        private static float T(int i) => i / (float)Rate;
        private static float Sample(float[] a, int i) => i >= 0 && i < a.Length ? a[i] : 0f;

        /// <summary>Attack-then-exponential-decay envelope.</summary>
        private static float Env(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));
        }

        /// <summary>Phase of a sine whose frequency glides exponentially from f0 to f1 (pitch-drop "thump").</summary>
        private static float Phase(float t, float f0, float f1, float glide)
        {
            float k = 1f / glide;
            // integral of f1 + (f0 - f1) e^{-kt}
            return 2f * Mathf.PI * (f1 * t + (f0 - f1) * (1f - Mathf.Exp(-k * t)) / k);
        }

        private static float[] Noise(System.Random rng, int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = (float)(rng.NextDouble() * 2 - 1);
            return a;
        }

        private static float[] Brown(System.Random rng, int n)
        {
            var a = new float[n];
            float v = 0f;
            for (int i = 0; i < n; i++)
            {
                v = Mathf.Clamp(v + (float)(rng.NextDouble() * 2 - 1) * 0.06f, -1f, 1f) * 0.998f;
                a[i] = v;
            }
            return a;
        }

        private static void Lowpass(float[] a, float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = (1f / Rate) / (rc + 1f / Rate);
            float y = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                y += alpha * (a[i] - y);
                a[i] = y;
            }
        }

        private static void Highpass(float[] a, float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = rc / (rc + 1f / Rate);
            float prevX = 0f, y = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                y = alpha * (y + a[i] - prevX);
                prevX = a[i];
                a[i] = y;
            }
        }

        private static void SweepLowpass(float[] a, float from, float to)
        {
            float y = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float cutoff = Mathf.Lerp(from, to, i / (float)a.Length);
                float rc = 1f / (2f * Mathf.PI * cutoff);
                float alpha = (1f / Rate) / (rc + 1f / Rate);
                y += alpha * (a[i] - y);
                a[i] = y;
            }
        }

        /// <summary>Crossfades the extra tail into the head so the loop has no click; drops the tail.</summary>
        private static float[] MakeSeamless(float[] a)
        {
            int fade = (int)(Fade * Rate);
            int len = a.Length - fade;
            var r = new float[len];
            Array.Copy(a, r, len);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                r[i] = a[len + i] * (1f - w) + a[i] * w;
            }
            return r;
        }

        /// <summary>Removes DC, fixes NaNs and normalises to the given peak. Relative loudness is set per sound by the AudioManager.</summary>
        private static void Sanitize(float[] a, float peak)
        {
            double mean = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) a[i] = 0f;
                mean += a[i];
            }
            mean /= Math.Max(1, a.Length);
            float max = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                a[i] -= (float)mean;
                max = Mathf.Max(max, Mathf.Abs(a[i]));
            }
            if (max > 1e-5f)
            {
                float g = peak / max;
                for (int i = 0; i < a.Length; i++) a[i] *= g;
            }
        }
    }
}
