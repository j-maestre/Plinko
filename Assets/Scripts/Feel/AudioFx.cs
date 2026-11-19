using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Sonidos generados por código (prototipo, sustituibles por SFX finales).
    /// Los rebotes suben por una escala pentatónica según la fila: la caída "toca una melodía".
    /// Limita cuántos sonidos suenan a la vez para que muchas bolas no saturen.
    /// </summary>
    public class AudioFx : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int Voices = 16;
        const int MaxPegSoundsPerWindow = 5;
        const float Window = 0.05f;

        static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };

        public float Volume = 0.6f;
        public bool Muted;
        /// <summary>Multiplicador de tono global (la Sobrecarga lo sube).</summary>
        public float PitchMul = 1f;

        AudioSource[] voices;
        int nextVoice;
        AudioClip pluck, chime, pop, sweep, launch, zap;
        float windowTimer;
        int pegPlays;

        void Awake()
        {
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0;
                voices[i] = src;
            }

            pluck = Tone("pluck", 523.25f, 0.18f, 28f, new[] { 1f, 0.35f, 0.12f });
            chime = Chord("chime", 523.25f, new[] { 1f, 1.5f, 2f }, 0.6f, 7f);
            pop = Tone("pop", 880f, 0.09f, 45f, new[] { 1f, 0.2f });
            launch = Tone("launch", 330f, 0.12f, 30f, new[] { 1f, 0.5f });
            sweep = Sweep("sweep", 440f, 1760f, 0.35f);
            zap = Zap("zap", 0.18f);

            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }

        void Update()
        {
            windowTimer += Time.unscaledDeltaTime;
            if (windowTimer >= Window) { windowTimer = 0; pegPlays = 0; }
        }

        static float Semitones(int s) => Mathf.Pow(2f, s / 12f);

        static int ScaleNote(int index)
        {
            int octave = index / Pentatonic.Length;
            return Pentatonic[index % Pentatonic.Length] + 12 * octave;
        }

        public void Peg(int row, int rows, int activeBalls)
        {
            if (pegPlays >= MaxPegSoundsPerWindow) return;
            pegPlays++;
            // con muchas bolas, cada rebote suena más bajito
            float vol = 0.35f / Mathf.Sqrt(Mathf.Max(1, activeBalls * 0.5f));
            int idx = rows <= 1 ? 0 : Mathf.RoundToInt(row * 10f / (rows - 1)); // dos octavas
            Play(pluck, Semitones(ScaleNote(idx) - 5) * Random.Range(0.995f, 1.005f), vol);
        }

        public void Special(float pitch = 1f) => Play(pop, pitch, 0.4f);

        /// <summary>Caída en cubo: más agudo y fuerte cuanto más lejos del centro.</summary>
        public void Land(int distance, int maxDistance)
        {
            float t = maxDistance == 0 ? 0 : (float)distance / maxDistance;
            Play(chime, Semitones(ScaleNote(Mathf.RoundToInt(t * 7))), 0.25f + 0.25f * t);
        }

        public void Crit() => Play(sweep, 1f, 0.45f);
        public void Record() => Play(sweep, 1.5f, 0.4f);
        public void Buy() => Play(chime, Semitones(12), 0.35f);
        public void Launch() => Play(launch, Random.Range(0.95f, 1.05f), 0.25f);
        public void Click() => Play(pop, 0.8f, 0.25f);
        public void Zap(float pitch) => Play(zap, pitch * Random.Range(0.95f, 1.05f), 0.3f);

        void Play(AudioClip clip, float pitch, float volume)
        {
            if (Muted) return;
            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            src.pitch = pitch * PitchMul;
            src.PlayOneShot(clip, volume * Volume);
        }

        // --- Síntesis ---

        static AudioClip Tone(string name, float freq, float dur, float decay, float[] harmonics)
        {
            int n = (int)(SampleRate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t / 0.002f);
                float v = 0;
                for (int h = 0; h < harmonics.Length; h++) v += harmonics[h] * Mathf.Sin(2f * Mathf.PI * freq * (h + 1) * t);
                data[i] = v * env * 0.5f;
            }
            return Make(name, data);
        }

        static AudioClip Chord(string name, float root, float[] ratios, float dur, float decay)
        {
            int n = (int)(SampleRate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float v = 0;
                for (int k = 0; k < ratios.Length; k++)
                {
                    float start = k * 0.035f; // arpegio rápido
                    if (t < start) continue;
                    float tt = t - start;
                    float env = Mathf.Exp(-tt * decay) * Mathf.Min(1f, tt / 0.003f);
                    v += env * (Mathf.Sin(2f * Mathf.PI * root * ratios[k] * tt) + 0.25f * Mathf.Sin(4f * Mathf.PI * root * ratios[k] * tt));
                }
                data[i] = v * 0.3f;
            }
            return Make(name, data);
        }

        static AudioClip Sweep(string name, float f0, float f1, float dur)
        {
            int n = (int)(SampleRate * dur);
            var data = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(f0, f1, t * t);
                phase += 2f * Mathf.PI * f / SampleRate;
                float env = Mathf.Sin(Mathf.PI * t) * (1f - t * 0.5f);
                data[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * env * 0.4f;
            }
            return Make(name, data);
        }

        /// <summary>Chasquido eléctrico: ruido con tono agudo que cae rápido.</summary>
        static AudioClip Zap(string name, float dur)
        {
            int n = (int)(SampleRate * dur);
            var data = new float[n];
            var rng = new System.Random(7);
            float phase = 0, hold = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                if (i % 6 == 0) hold = (float)(rng.NextDouble() * 2 - 1); // ruido "crujiente"
                phase += 2f * Mathf.PI * Mathf.Lerp(2400f, 600f, t) / SampleRate;
                float env = Mathf.Exp(-t * 9f) * Mathf.Min(1f, i / 40f);
                data[i] = (0.6f * hold + 0.4f * Mathf.Sin(phase)) * env * 0.5f;
            }
            return Make(name, data);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
