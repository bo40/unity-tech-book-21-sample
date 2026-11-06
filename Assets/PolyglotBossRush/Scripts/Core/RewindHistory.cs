using UnityEngine;

namespace PolyglotBossRush
{
    // ある瞬間のゲーム全体の状態。ボスの状態（BossState）を保存できるかどうかは言語しだい
    public sealed class GameSnapshot
    {
        public Bullet[] Bullets = new Bullet[1024];
        public int BulletCount;
        public Vector2 PlayerPosition;
        public float ShotTimer;
        public readonly Bullet[] Shots = new Bullet[64];
        public int ShotCount;
        public float BossTime;
        public float BossHp;
        public object BossState;
    }

    // 直近の状態を一定間隔で積むリングバッファ。古いものから上書きし、新しいものから取り出す
    public sealed class RewindHistory
    {
        readonly GameSnapshot[] snapshots;
        int newest = -1;

        public int Count { get; private set; }
        public int Capacity => snapshots.Length;

        public RewindHistory(int capacity)
        {
            snapshots = new GameSnapshot[capacity];
            for (var i = 0; i < capacity; i++)
                snapshots[i] = new GameSnapshot();
        }

        /// <summary>書き込む先のスナップショットを返す（いちばん古いものを使い回す）</summary>
        public GameSnapshot Push()
        {
            newest = (newest + 1) % snapshots.Length;
            Count = Mathf.Min(Count + 1, snapshots.Length);
            return snapshots[newest];
        }

        public bool TryPop(out GameSnapshot snapshot)
        {
            if (Count == 0)
            {
                snapshot = null;
                return false;
            }
            snapshot = snapshots[newest];
            newest = (newest - 1 + snapshots.Length) % snapshots.Length;
            Count--;
            return true;
        }

        public void Clear()
        {
            Count = 0;
            newest = -1;
        }
    }
}
