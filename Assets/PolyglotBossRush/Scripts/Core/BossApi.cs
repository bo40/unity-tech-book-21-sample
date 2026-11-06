using UnityEngine;

namespace PolyglotBossRush
{
    // ボスから見えるゲームの窓口。C# と F# のボスはこれを直接使い、
    // ネイティブのボスは UniBridge（C の関数ポインタ表）経由でここに届く
    public sealed class BossApi
    {
        /// <summary>UniBridge のコールバックが転送する先。いま戦っているボスの BossApi</summary>
        public static BossApi Current { get; set; }

        readonly BulletWorld world;
        public Vector2 BossPosition { get; set; }
        public Vector2 PlayerPosition { get; set; }
        public float HpRatio { get; set; } = 1f;
        public int SpawnedThisFrame { get; set; }

        public BossApi(BulletWorld world) => this.world = world;

        /// <summary>ボスの位置からの相対座標 (x, y) に弾を出す</summary>
        public void Spawn(float x, float y, float angle, float speed, float accel, uint rgba)
        {
            world.Add(BossPosition + new Vector2(x, y), angle, speed, accel, rgba);
            SpawnedThisFrame++;
        }

        /// <summary>ボスから見た自機の方向（ラジアン）</summary>
        public float AimAngle
        {
            get
            {
                var direction = PlayerPosition - BossPosition;
                return Mathf.Atan2(direction.y, direction.x);
            }
        }

        public static uint Rgba(Color32 color) => (uint)(color.r << 24 | color.g << 16 | color.b << 8 | color.a);
    }
}
