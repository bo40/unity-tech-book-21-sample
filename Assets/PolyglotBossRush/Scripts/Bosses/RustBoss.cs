using System;
using System.Runtime.InteropServices;

namespace PolyglotBossRush
{
    // ボス 3: Rust ─ 重力井戸。弾を撒くのも、井戸に引き寄せられる弾を動かすのも Rust（langs/rust）
    public sealed unsafe class RustBoss : Boss
    {
        [DllImport("unihub")] static extern IntPtr rust_boss_new(UniBridge bridge);
        [DllImport("unihub")] static extern int rust_boss_step(IntPtr boss);
        [DllImport("unihub")] static extern nuint rust_boss_wells(IntPtr boss, Well* buffer, nuint capacity);
        [DllImport("unihub")] static extern void rust_boss_free(IntPtr boss);
        [DllImport("unihub")] static extern uint rust_boss_frame(IntPtr boss);
        [DllImport("unihub")] static extern void rust_boss_set_frame(IntPtr boss, uint frame);

        IntPtr handle;

        public override string Language => "Rust";
        public override string Title => "Gravity Wells";

        public override void Begin(BossApi api) => handle = rust_boss_new(UniBridge.Create());

        public override bool Step()
        {
            if (rust_boss_step(handle) < 0)
                throw new BossException("rust_boss_step: " + NativeError.Last());
            return false;
        }

        // Rust のボスの状態は普通の struct（フレーム数）なので、そのまま保存して戻せる
        public override string RewindUnsupportedReason => null;
        public override object SaveState(object reuse) => rust_boss_frame(handle);
        public override void LoadState(object state) => rust_boss_set_frame(handle, (uint)state);

        public override int GetWells(Well[] buffer)
        {
            fixed (Well* pointer = buffer)
                return (int)rust_boss_wells(handle, pointer, (nuint)buffer.Length);
        }

        public override void Dispose()
        {
            rust_boss_free(handle);
            handle = IntPtr.Zero;
        }
    }
}
