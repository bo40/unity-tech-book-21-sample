using UnityEngine;

namespace PolyglotBossRush
{
    // 被弾と巻き戻しの効果音。音声ファイルは使わず、その場で波形を作る
    public sealed class Sfx
    {
        const int SampleRate = 44100;
        readonly AudioSource source;
        readonly AudioClip hit;
        readonly AudioClip rewind;

        public Sfx(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            hit = Synthesize("hit", 0.22f, (t, random) =>
                // ノイズと低い矩形波を、すばやく減衰させる
                ((float)random.NextDouble() * 2f - 1f) * 0.6f * Mathf.Exp(-t * 18f)
                + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 90f * t)) * 0.4f * Mathf.Exp(-t * 10f));
            rewind = Synthesize("rewind", 0.4f, (t, random) =>
                // 高い音から低い音へ下がるうなり（テープの逆回し風）
                Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1400f, 180f, t / 0.4f) * t) * 0.35f * Mathf.Sin(Mathf.PI * t / 0.4f));
        }

        static AudioClip Synthesize(string name, float seconds, System.Func<float, System.Random, float> wave)
        {
            var random = new System.Random(1);
            var samples = new float[(int)(SampleRate * seconds)];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = wave(i / (float)SampleRate, random);
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlayHit() => source.PlayOneShot(hit, 0.9f);
        public void PlayRewind() => source.PlayOneShot(rewind, 0.6f);
    }

    // 被弾の画面効果: 火花、自機の周りの閃光、画面の揺れ
    public sealed class HitEffects
    {
        const int MaxSparks = 256;
        const float SparkLife = 0.5f;
        const float FlashLife = 0.3f;
        const float ShakeLife = 0.3f;

        public readonly Bullet[] Sparks = new Bullet[MaxSparks];
        public int SparkCount { get; private set; }
        public readonly Bullet[] Flash = new Bullet[1];
        public bool FlashVisible => flashTimer > 0f;

        readonly float[] sparkTimers = new float[MaxSparks];
        readonly System.Random random = new System.Random(7);
        float flashTimer;
        float shakeTimer;

        public Vector2 ShakeOffset =>
            shakeTimer <= 0f ? Vector2.zero : new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 0.3f * (shakeTimer / ShakeLife);

        public void Burst(Vector2 at)
        {
            for (var i = 0; i < 24 && SparkCount < MaxSparks; i++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var speed = 2f + (float)random.NextDouble() * 4f;
                sparkTimers[SparkCount] = SparkLife;
                Sparks[SparkCount++] = new Bullet
                {
                    X = at.x, Y = at.y,
                    VelocityX = Mathf.Cos(angle) * speed, VelocityY = Mathf.Sin(angle) * speed,
                    Rgba = 0xFFB040FF,
                };
            }
            Flash[0] = new Bullet { X = at.x, Y = at.y, Rgba = 0xFF6060FF };
            flashTimer = FlashLife;
            shakeTimer = ShakeLife;
        }

        public void Update(float dt)
        {
            for (var i = 0; i < SparkCount;)
            {
                sparkTimers[i] -= dt;
                if (sparkTimers[i] <= 0f)
                {
                    SparkCount--;
                    Sparks[i] = Sparks[SparkCount];
                    sparkTimers[i] = sparkTimers[SparkCount];
                    continue;
                }
                ref var spark = ref Sparks[i];
                spark.X += spark.VelocityX * dt;
                spark.Y += spark.VelocityY * dt;
                spark.Rgba = spark.Rgba & 0xFFFFFF00u | (uint)(255f * sparkTimers[i] / SparkLife);
                i++;
            }
            flashTimer -= dt;
            shakeTimer -= dt;
            Flash[0].Rgba = Flash[0].Rgba & 0xFFFFFF00u | (uint)(200f * Mathf.Clamp01(flashTimer / FlashLife));
        }
    }
}
