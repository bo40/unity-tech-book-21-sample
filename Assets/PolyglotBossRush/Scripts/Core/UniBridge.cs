using System;
using System.Runtime.InteropServices;
using AOT;

namespace PolyglotBossRush
{
    // langs/common/unibridge.h の C# 版。すべてのネイティブのボスが見る、唯一の Unity 窓口
    [StructLayout(LayoutKind.Sequential)]
    public struct UniBridge
    {
        public IntPtr Spawn;
        public IntPtr HpRatio;
        public IntPtr AimAngle;
        public IntPtr User;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void SpawnCallback(float x, float y, float angle, float speed, float accel, uint rgba, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate float FloatQuery(IntPtr user);

        // IL2CPP でネイティブから呼ばれる C# 関数は static で、[MonoPInvokeCallback] を付ける。
        // （Unity 6.6 では [UnmanagedCallersOnly] が internal 扱いで使えない）
        [MonoPInvokeCallback(typeof(SpawnCallback))]
        static void OnSpawn(float x, float y, float angle, float speed, float accel, uint rgba, IntPtr user) =>
            BossApi.Current.Spawn(x, y, angle, speed, accel, rgba);

        [MonoPInvokeCallback(typeof(FloatQuery))]
        static float OnHpRatio(IntPtr user) => BossApi.Current.HpRatio;

        [MonoPInvokeCallback(typeof(FloatQuery))]
        static float OnAimAngle(IntPtr user) => BossApi.Current.AimAngle;

        // デリゲートが GC に回収されると関数ポインタが宙に浮くので、static に持っておく
        static readonly SpawnCallback SpawnDelegate = OnSpawn;
        static readonly FloatQuery HpRatioDelegate = OnHpRatio;
        static readonly FloatQuery AimAngleDelegate = OnAimAngle;

        public static UniBridge Create() => new UniBridge
        {
            Spawn = Marshal.GetFunctionPointerForDelegate(SpawnDelegate),
            HpRatio = Marshal.GetFunctionPointerForDelegate(HpRatioDelegate),
            AimAngle = Marshal.GetFunctionPointerForDelegate(AimAngleDelegate),
        };
    }
}
