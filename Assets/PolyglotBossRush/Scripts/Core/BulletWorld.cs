using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PolyglotBossRush
{
    // 弾の配列を持ち、毎フレームの更新を Rust の弾幕エンジンに任せる。
    // 配列は C# が持ったまま、ポインタだけを渡す（コピーしない）
    public sealed unsafe class BulletWorld
    {
        public const int Capacity = 100_000;

        [DllImport("unihub")]
        static extern nuint danmaku_step(Bullet* bullets, nuint bulletCount, Well* wells, nuint wellCount,
            Arena* arena, float dt, int parallel, uint* hits);

        [DllImport("unihub")] static extern int danmaku_thread_count();

        public readonly Bullet[] Bullets = new Bullet[Capacity];
        public int Count { get; private set; }
        public int ThreadCount => danmaku_thread_count();

        public void Add(Vector2 position, float angle, float speed, float accel, uint rgba)
        {
            if (Count == Capacity)
                return;
            Bullets[Count++] = new Bullet
            {
                X = position.x,
                Y = position.y,
                VelocityX = Mathf.Cos(angle) * speed,
                VelocityY = Mathf.Sin(angle) * speed,
                Accel = accel,
                Rgba = rgba,
            };
        }

        public void Clear() => Count = 0;

        /// <summary>巻き戻し用に、いまの弾をすべて snapshot にコピーする</summary>
        public void SaveTo(GameSnapshot snapshot)
        {
            if (snapshot.Bullets.Length < Count)
                snapshot.Bullets = new Bullet[Mathf.NextPowerOfTwo(Count)];
            Array.Copy(Bullets, snapshot.Bullets, Count);
            snapshot.BulletCount = Count;
        }

        public void LoadFrom(GameSnapshot snapshot)
        {
            Array.Copy(snapshot.Bullets, Bullets, snapshot.BulletCount);
            Count = snapshot.BulletCount;
        }

        /// <summary>弾を 1 フレーム進め、画面外の弾と自機に当たった弾を消す。当たった数を返す</summary>
        public int Step(Well[] wells, int wellCount, Arena arena, float dt)
        {
            uint hits;
            fixed (Bullet* bullets = Bullets)
            fixed (Well* wellPointer = wells)
                Count = (int)danmaku_step(bullets, (nuint)Count, wellPointer, (nuint)wellCount, &arena, dt, 1, &hits);
            return (int)hits;
        }
    }
}
