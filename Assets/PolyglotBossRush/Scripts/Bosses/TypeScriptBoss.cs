using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PolyglotBossRush
{
    // ボス 4: TypeScript ─ esbuild で 1 ファイルにした JS（langs/typescript）を、
    // Rust の unihub に組み込んだ QuickJS-ng で動かす
    public sealed unsafe class TypeScriptBoss : Boss
    {
        [DllImport("unihub")] static extern IntPtr js_boss_new(byte* source, nuint length, UniBridge bridge);
        [DllImport("unihub")] static extern int js_boss_step(IntPtr boss);
        [DllImport("unihub")] static extern void js_boss_free(IntPtr boss);

        IntPtr handle;

        public override string Language => "TypeScript";
        public override string Title => "CSS Fireworks";

        public override void Begin(BossApi api)
        {
            var source = Encoding.UTF8.GetBytes(Resources.Load<TextAsset>("Bosses/typescript").text);
            fixed (byte* pointer = source)
                handle = js_boss_new(pointer, (nuint)source.Length, UniBridge.Create());
            if (handle == IntPtr.Zero)
                throw new BossException("js_boss_new: " + NativeError.Last());
        }

        // await の中断状態は QuickJS のヒープ上のクロージャとジョブキューにあり、取り出して保存する手段がない
        public override string RewindUnsupportedReason =>
            "TypeScript：中断状態が QuickJS（JavaScript のランタイム）の中にあって取り出せないため時間を巻き戻せません";

        public override bool Step()
        {
            var status = js_boss_step(handle);
            if (status < 0)
                throw new BossException("js_boss_step: " + NativeError.Last());
            return status == 1;
        }

        public override void Dispose()
        {
            js_boss_free(handle);
            handle = IntPtr.Zero;
        }
    }
}
