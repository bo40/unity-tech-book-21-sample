using System.Runtime.InteropServices;

namespace PolyglotBossRush
{
    // Rust の danmaku::Bullet、シェーダーの Bullet と同じ並び（24 バイト）
    [StructLayout(LayoutKind.Sequential)]
    public struct Bullet
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        /// <summary>毎秒の速さの増減（進行方向）。負なら減速して止まる</summary>
        public float Accel;
        /// <summary>0xRRGGBBAA</summary>
        public uint Rgba;
    }

    // 重力井戸。Rust の danmaku::Well と同じ並び
    [StructLayout(LayoutKind.Sequential)]
    public struct Well
    {
        public float X, Y, Strength, Padding;
    }

    // 弾が生きていられる範囲と自機の当たり判定。Rust の danmaku::Arena と同じ並び
    [StructLayout(LayoutKind.Sequential)]
    public struct Arena
    {
        public float MinX, MinY, MaxX, MaxY;
        public float PlayerX, PlayerY;
        public float HitRadius;
        public float Padding;
    }
}
