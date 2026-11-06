using System;

namespace PolyglotBossRush
{
    // 1 体のボス。言語ごとに、その言語の処理系を Unity につなぐアダプタを書く
    public abstract class Boss : IDisposable
    {
        public abstract string Language { get; }
        public abstract string Title { get; }

        /// <summary>弾幕を始める。ネイティブのボスはここで処理系を立ち上げる</summary>
        public abstract void Begin(BossApi api);

        /// <summary>1 フレーム進める。弾幕を撃ち終えたら true（ゲーム側が最初からやり直させる）</summary>
        public abstract bool Step();

        /// <summary>重力井戸の配置（ボスからの相対座標）。井戸を使うボスだけが上書きする</summary>
        public virtual int GetWells(Well[] buffer) => 0;

        // ---- 時間の巻き戻し ----
        // 巻き戻すには「コルーチンの途中の状態」を保存して戻せないといけない。できるかどうかは言語しだい

        /// <summary>巻き戻せないなら、その理由（画面に出すので、記事と同じやさしい日本語で書く）。巻き戻せるなら null</summary>
        public abstract string RewindUnsupportedReason { get; }

        /// <summary>いまの状態を保存する。reuse は以前に返した値（使い回してよい）</summary>
        public virtual object SaveState(object reuse) => null;

        public virtual void LoadState(object state) { }

        public virtual void Dispose() { }
    }

    public sealed class BossException : Exception
    {
        public BossException(string message) : base(message) { }
    }
}
