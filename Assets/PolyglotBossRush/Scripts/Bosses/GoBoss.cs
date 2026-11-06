using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PolyglotBossRush
{
    // ボス 7: Go ─ GOOS=wasip1 で WASI モジュールにした Go（langs/go）を、OCaml と同じく
    // Rust の unihub に組み込んだ wasmtime で動かす。端末には JIT を載せず、事前コンパイルした .cwasm を読む
    public sealed unsafe class GoBoss : Boss
    {
        [DllImport("unihub")] static extern IntPtr wasm_boss_new(byte* bytes, nuint length, UniBridge bridge);
        [DllImport("unihub")] static extern int wasm_boss_step(IntPtr boss);
        [DllImport("unihub")] static extern void wasm_boss_free(IntPtr boss);

#if UNITY_ANDROID
        const string ModuleResource = "Bosses/go_android";
#else
        const string ModuleResource = "Bosses/go_macos";
#endif

        IntPtr handle;

        public override string Language => "Go";
        public override string Title => "Goroutine Swarm";

        public override void Begin(BossApi api)
        {
            var module = Resources.Load<TextAsset>(ModuleResource).bytes;
            fixed (byte* pointer = module)
                handle = wasm_boss_new(pointer, (nuint)module.Length, UniBridge.Create());
            if (handle == IntPtr.Zero)
                throw new BossException("wasm_boss_new: " + NativeError.Last());
        }

        // 1,000 個の goroutine のスタックは、Wasm の線形メモリの中で Go のランタイムが管理している。ボスごとに取り出す手段はない
        public override string RewindUnsupportedReason =>
            "Go：1,000 個の goroutine の中断状態が Go のランタイムの中にあって取り出せないため時間を巻き戻せません";

        public override bool Step()
        {
            var status = wasm_boss_step(handle);
            if (status < 0)
                throw new BossException("wasm_boss_step: " + NativeError.Last());
            return status == 1;
        }

        public override void Dispose()
        {
            wasm_boss_free(handle);
            handle = IntPtr.Zero;
        }
    }
}
