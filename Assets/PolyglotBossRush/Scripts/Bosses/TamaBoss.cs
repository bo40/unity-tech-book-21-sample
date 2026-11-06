using System;
using System.Runtime.InteropServices;

namespace PolyglotBossRush
{
    // ラスボス: オレオレ言語 Tama ─ langs/tama のコンパイラが LLVM でネイティブコードにしたボス。
    // tama_step は wait を境に状態機械に変換されている
    public sealed unsafe class TamaBoss : Boss
    {
        [DllImport("tama_ore")] static extern IntPtr tama_new();
        [DllImport("tama_ore")] static extern int tama_step(IntPtr state, UniBridge* bridge);
        [DllImport("tama_ore")] static extern void tama_free(IntPtr state);
        [DllImport("tama_ore")] static extern long tama_state_size();

        IntPtr state;
        UniBridge bridge;

        public override string Language => "Tama";
        public override string Title => "The Last Boss";

        public override void Begin(BossApi api)
        {
            bridge = UniBridge.Create();
            state = tama_new();
        }

        // Tama はすべての変数と「どの wait で止まっているか」をフレーム構造体に置く言語として設計した。
        // だから状態はただのメモリで、コピーするだけで保存も巻き戻しもできる
        public override string RewindUnsupportedReason => null;

        public override object SaveState(object reuse)
        {
            var size = (int)tama_state_size();
            var buffer = reuse is byte[] bytes && bytes.Length == size ? bytes : new byte[size];
            Marshal.Copy(state, buffer, 0, size);
            return buffer;
        }

        public override void LoadState(object saved)
        {
            var buffer = (byte[])saved;
            Marshal.Copy(buffer, 0, state, buffer.Length);
        }

        public override bool Step()
        {
            fixed (UniBridge* pointer = &bridge)
                return tama_step(state, pointer) != 0;
        }

        public override void Dispose()
        {
            tama_free(state);
            state = IntPtr.Zero;
        }
    }
}
