using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PolyglotBossRush
{
    // ボス 5: Python ─ 本物の CPython を埋め込み（langs/python/pybridge.c）、generator のボスを 1 フレームずつ進める
    public sealed class PythonBoss : Boss
    {
        [DllImport("pybridge")] static extern int pybridge_init(string home);
        [DllImport("pybridge")] static extern int pybridge_load(string source, UniBridge bridge);
        [DllImport("pybridge")] static extern int pybridge_step();
        [DllImport("pybridge")] static extern IntPtr pybridge_last_error();

        public override string Language => "Python";
        public override string Title => "Bullet Banner";

        public override void Begin(BossApi api)
        {
            if (PythonHome.Path == null)
                throw new BossException("Python's standard library is not ready");
            if (pybridge_init(PythonHome.Path) != 0)
                throw new BossException("pybridge_init: " + LastError());
            if (pybridge_load(Resources.Load<TextAsset>("Bosses/python").text, UniBridge.Create()) != 0)
                throw new BossException("pybridge_load: " + LastError());
        }

        // CPython の generator はコピーも pickle もできない（TypeError: cannot pickle 'generator' object）
        public override string RewindUnsupportedReason =>
            "Python：中断状態が CPython（Python のランタイム）の中にあって取り出せないため時間を巻き戻せません";

        public override bool Step()
        {
            var status = pybridge_step();
            if (status < 0)
                throw new BossException("pybridge_step: " + LastError());
            return status == 1;
        }

        static string LastError() => Marshal.PtrToStringUTF8(pybridge_last_error());
    }
}
