using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace PolyglotBossRush
{
    // CPython の home（lib/python3.14 を含むディレクトリ）を用意する
    public static class PythonHome
    {
        public static string Path { get; private set; }

        public static IEnumerator Prepare()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // 標準ライブラリは APK 内の StreamingAssets に zip で入れてあるので、初回だけ展開する。
            // persistentDataPath は外部ストレージ（noexec）を指すことがあり、lib-dynload の .so を dlopen できない。
            // そのためアプリ内部の filesDir に展開する
            var home = System.IO.Path.Combine(InternalFilesDirectory(), "python");
            if (!File.Exists(System.IO.Path.Combine(home, "lib/python3.14/os.py")))
            {
                using var request = UnityWebRequest.Get(System.IO.Path.Combine(Application.streamingAssetsPath, "python-stdlib.zip"));
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[BossRush] failed to read python-stdlib.zip: " + request.error);
                    yield break;
                }
                using var zip = new ZipArchive(new MemoryStream(request.downloadHandler.data));
                zip.ExtractToDirectory(home);
            }
            Path = home;
#else
            // macOS（Editor とスタンドアロン）では Homebrew の Python 3.14 を使う
            Path = "/opt/homebrew/opt/python@3.14/Frameworks/Python.framework/Versions/3.14";
            yield break;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static string InternalFilesDirectory()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var filesDir = activity.Call<AndroidJavaObject>("getFilesDir");
            return filesDir.Call<string>("getAbsolutePath");
        }
#endif
    }
}
