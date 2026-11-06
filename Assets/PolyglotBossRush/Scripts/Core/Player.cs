using System;
using UnityEngine;

namespace PolyglotBossRush
{
    // 自機。Controls の方向に動き、自動で上に撃つ
    public sealed class Player
    {
        public const float Radius = 0.12f;
        const float Speed = 5f;
        const float ShotSpeed = 14f;
        const float ShotInterval = 1f / 12f;
        const int MaxShots = 64;
        static readonly Vector2 StartPosition = new Vector2(0f, -3.5f);

        public Vector2 Position = StartPosition;
        public readonly Bullet[] Shots = new Bullet[MaxShots];
        public int ShotCount { get; private set; }

        float shotTimer;
        float autoplayTime;

        public void Reset()
        {
            Position = StartPosition;
            ShotCount = 0;
            shotTimer = 0f;
        }

        public void Update(float dt, Rect bounds, bool autoplay, Vector2 direction)
        {
            if (autoplay)
                Position = AutoplayPosition(dt);
            else
                Position += Vector2.ClampMagnitude(direction, 1f) * Speed * dt;
            Position.x = Mathf.Clamp(Position.x, bounds.xMin + 0.3f, bounds.xMax - 0.3f);
            Position.y = Mathf.Clamp(Position.y, bounds.yMin + 0.3f, bounds.yMax - 0.3f);
            UpdateShots(dt, bounds);
        }

        // デモと計測用: 画面下を左右にゆらゆら動く。
        // autoplayTime は巻き戻さないので、巻き戻したあとは前回と違う動きになる
        Vector2 AutoplayPosition(float dt)
        {
            autoplayTime += dt;
            return new Vector2(Mathf.Sin(autoplayTime * 0.8f) * 1.8f, -3.5f + Mathf.Sin(autoplayTime * 0.37f) * 0.6f);
        }

        void UpdateShots(float dt, Rect bounds)
        {
            shotTimer -= dt;
            if (shotTimer <= 0f && ShotCount < MaxShots)
            {
                shotTimer += ShotInterval;
                Shots[ShotCount++] = new Bullet { X = Position.x, Y = Position.y + 0.2f, VelocityY = ShotSpeed, Rgba = 0x80FFB0FF };
            }
            for (var i = 0; i < ShotCount;)
            {
                Shots[i].Y += Shots[i].VelocityY * dt;
                if (Shots[i].Y > bounds.yMax)
                    Shots[i] = Shots[--ShotCount];
                else
                    i++;
            }
        }

        /// <summary>ボスに当たった自機の弾を消し、当たった数を返す</summary>
        public int CollectHits(Vector2 bossPosition, float bossRadius)
        {
            var hits = 0;
            for (var i = 0; i < ShotCount;)
            {
                if ((new Vector2(Shots[i].X, Shots[i].Y) - bossPosition).sqrMagnitude < bossRadius * bossRadius)
                {
                    Shots[i] = Shots[--ShotCount];
                    hits++;
                }
                else
                {
                    i++;
                }
            }
            return hits;
        }

        public void SaveTo(GameSnapshot snapshot)
        {
            snapshot.PlayerPosition = Position;
            snapshot.ShotTimer = shotTimer;
            Array.Copy(Shots, snapshot.Shots, ShotCount);
            snapshot.ShotCount = ShotCount;
        }

        public void LoadFrom(GameSnapshot snapshot)
        {
            Position = snapshot.PlayerPosition;
            shotTimer = snapshot.ShotTimer;
            Array.Copy(snapshot.Shots, Shots, snapshot.ShotCount);
            ShotCount = snapshot.ShotCount;
        }
    }
}
