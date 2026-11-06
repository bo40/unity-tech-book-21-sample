using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PolyglotBossRush
{
    // ボス 1: C# ─ 基準となる実装。全方位弾を回しながら撃つ。
    // 「待つ」は yield return。コンパイラがこのメソッドを状態機械のクラスに変換している
    public sealed class CSharpBoss : Boss
    {
        const int BulletsPerRing = 36;
        const float Speed = 2.5f;

        BossApi api;
        IEnumerator<int> pattern;
        int framesToWait;

        public override string Language => "C#";
        public override string Title => "Spiral";

        public override void Begin(BossApi api)
        {
            this.api = api;
            pattern = Pattern().GetEnumerator();
        }

        IEnumerable<int> Pattern()
        {
            for (var wave = 0; ; wave++)
            {
                var color = BossApi.Rgba(Color.HSVToRGB(wave * 0.07f % 1f, 0.6f, 1f));
                for (var i = 0; i < BulletsPerRing; i++)
                {
                    var angle = 2f * Mathf.PI * i / BulletsPerRing + wave * 0.1f;
                    api.Spawn(0f, 0f, angle, Speed, 0f, color);
                }
                yield return 20; // 20 フレーム待つ
            }
        }

        // C# の yield の状態は、コンパイラが作ったクラスのフィールド（いまの位置、wave、i、color）にある。
        // そのクラスは外から見えないが、リフレクションで MemberwiseClone を呼べば丸ごと複製できる（反則技）
        static readonly MethodInfo MemberwiseClone =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        static IEnumerator<int> Clone(IEnumerator<int> iterator) => (IEnumerator<int>)MemberwiseClone.Invoke(iterator, null);

        public override string RewindUnsupportedReason => null;

        public override object SaveState(object reuse) => (Clone(pattern), framesToWait);

        public override void LoadState(object state)
        {
            var (saved, frames) = ((IEnumerator<int>, int))state;
            pattern = Clone(saved);
            framesToWait = frames;
        }

        public override bool Step()
        {
            if (framesToWait > 0)
            {
                framesToWait--;
                return false;
            }
            if (!pattern.MoveNext())
                return true;
            framesToWait = pattern.Current - 1;
            return false;
        }
    }
}
