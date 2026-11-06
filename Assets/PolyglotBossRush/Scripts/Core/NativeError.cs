using System.Runtime.InteropServices;
using System.Text;

namespace PolyglotBossRush
{
    // Rust の unihub が記録した直近のエラーを読む
    public static unsafe class NativeError
    {
        [DllImport("unihub")] static extern nuint unihub_last_error(byte* buffer, nuint capacity);

        public static string Last()
        {
            var buffer = new byte[4096];
            fixed (byte* pointer = buffer)
            {
                var length = (int)unihub_last_error(pointer, (nuint)buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
        }
    }
}
