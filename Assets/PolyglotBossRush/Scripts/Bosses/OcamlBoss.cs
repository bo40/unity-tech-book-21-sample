using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PolyglotBossRush
{
    // ボス 6: OCaml ─ wasm_of_ocaml で WASI モジュールにした OCaml（langs/ocaml）を、
    // Rust の unihub に組み込んだ wasmtime で動かす。端末には JIT を載せず、事前コンパイルした .cwasm を読む
    public sealed unsafe class OcamlBoss : Boss
    {
        [DllImport("unihub")] static extern IntPtr wasm_boss_new(byte* bytes, nuint length, UniBridge bridge);
        [DllImport("unihub")] static extern int wasm_boss_step(IntPtr boss);
        [DllImport("unihub")] static extern void wasm_boss_free(IntPtr boss);

#if UNITY_ANDROID
        const string ModuleResource = "Bosses/ocaml_android";
#else
        const string ModuleResource = "Bosses/ocaml_macos";
#endif

        IntPtr handle;

        public override string Language => "OCaml";
        public override string Title => "Fibonacci Sunflower";

        public override void Begin(BossApi api)
        {
            var module = Resources.Load<TextAsset>(ModuleResource).bytes;
            fixed (byte* pointer = module)
                handle = wasm_boss_new(pointer, (nuint)module.Length, UniBridge.Create());
            if (handle == IntPtr.Zero)
                throw new BossException("wasm_boss_new: " + NativeError.Last());
        }

        // OCaml 5 の継続は一度しか再開できない（one-shot）。さらに実体は Wasm の GC ヒープと fiber のスタックにある
        public override string RewindUnsupportedReason =>
            "OCaml：中断状態が wasmtime（Wasm のランタイム）の中にあって取り出せないため時間を巻き戻せません";

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
