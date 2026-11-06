using System.Collections.Generic;
using BossFSharp;
using FSharpPatterns = BossFSharp.Boss;

namespace PolyglotBossRush
{
    // ボス 2: F# ─ 弾幕は「値」。F# 側（langs/fsharp）が Pattern を解釈して Command の列を返し、
    // C# はそれを 1 フレームずつ実行するだけ。IL の道なので、DLL を置けば普通のクラスとして見える
    public sealed class FSharpBoss : Boss
    {
        BossApi api;
        IEnumerator<Command> commands;
        int framesToWait;

        public override string Language => "F#";
        public override string Title => "Pattern Algebra";

        public override void Begin(BossApi api)
        {
            this.api = api;
            commands = FSharpPatterns.create(() => api.HpRatio).GetEnumerator();
        }

        // seq { } は入れ子の列挙子（yield! のたびに 1 段）を内部で共有しているので、浅いコピーでは戻せない
        public override string RewindUnsupportedReason =>
            "F#：中断状態が入れ子のオブジェクトに分かれていて、丸ごと写せないため時間を巻き戻せません";

        public override bool Step()
        {
            if (framesToWait > 0)
            {
                framesToWait--;
                return false;
            }
            while (commands.MoveNext())
            {
                // F# の判別共用体は、C# からはケースごとの入れ子クラスに見える
                switch (commands.Current)
                {
                    case Command.Spawn spawn:
                        api.Spawn(0f, 0f, (float)spawn.angle, (float)spawn.speed, 0f, spawn.rgba);
                        break;
                    case Command.Sleep sleep:
                        framesToWait = sleep.Item - 1;
                        return false;
                }
            }
            return true;
        }
    }
}
