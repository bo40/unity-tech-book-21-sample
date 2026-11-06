using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PolyglotBossRush
{
    // 8 言語ボスラッシュの本体。ボスを順番に呼び出し、弾の更新は Rust、描画は GPU に任せる。
    // REWIND を押しているあいだは時間が巻き戻る。ただし、ボスの状態を保存できる言語のときだけ。
    //
    // コマンドライン引数:
    //   -autoplay          自機を自動で動かす
    //   -boss <n>          n 番目（0 始まり）のボスから始める
    //   -capture <dir>     各ボスの 5 秒目と、最初に被弾した直後の画面を保存し、全員分を撮ったら終了する（autoplay と一緒に使う）。
    //                      Android では persistentDataPath からの相対パス
    //   -rewind-at <sec>   autoplay のとき、各ボスのこの時刻に 1 回だけ自動で巻き戻す（巻き戻し中の画面も撮る）
    //   -boss-hp <ratio>   最初のボスの HP をこの割合（0〜1）から始める。後半のフェーズを撮るため
    //   -capture-at <sec>  -capture で撮る時刻（既定は 5 秒目）
    // これらはデモと撮影用。何か操作されたらすべて止めて、手動で遊べるようにする
    public sealed class BossRushGame : MonoBehaviour
    {
        const float BossMaxHp = 120f;
        const float BossRadius = 0.6f;
        const float BulletRadius = 0.07f;
        float captureAtSeconds = 5f;
        // 縦に長い画面（21:9 など）でも、この横幅（ワールド単位の半分）は必ず見えるようにする
        const float MinVisibleHalfWidth = 3.2f;
        const float MinVisibleHalfHeight = 5f;
        // ボスは「n フレーム待つ」で書かれているので、ゲームの更新は 60Hz の固定ステップで回す
        const float TickSeconds = 1f / 60f;
        const int MaxTicksPerFrame = 4;
        // 2 tick ごとに状態を保存し、5 秒分さかのぼれるようにする
        const int TicksPerSnapshot = 2;
        const int RewindSeconds = 5;
        const int AutoRewindSnapshots = 60;
        const float InvincibleSeconds = 1f;
        const float RewindSoundInterval = 0.4f;

        static readonly Color Background = new Color(0.03f, 0.02f, 0.08f);
        static readonly Color RewindBackground = new Color(0.14f, 0.09f, 0.03f);

        static readonly Func<Boss>[] BossFactories =
        {
            () => new CSharpBoss(),
            () => new FSharpBoss(),
            () => new RustBoss(),
            () => new TypeScriptBoss(),
            () => new PythonBoss(),
            () => new OcamlBoss(),
            () => new GoBoss(),
            () => new TamaBoss(),
        };

        readonly BulletWorld world = new BulletWorld();
        readonly Player player = new Player();
        readonly Controls controls = new Controls();
        readonly HitEffects effects = new HitEffects();
        readonly RewindHistory history = new RewindHistory(RewindSeconds * 60 / TicksPerSnapshot);
        readonly Well[] wells = new Well[8];
        readonly Bullet[] single = new Bullet[1];
        CircleRenderer bulletRenderer, shotRenderer, playerRenderer, bossRenderer, sparkRenderer, flashRenderer;
        BossApi api;
        Camera mainCamera;
        Sfx sfx;

        Boss boss;
        int bossIndex;
        float bossHp;
        float bossTime;
        int tickCount;
        int playerHits;
        float invincibleTimer;
        bool ready;
        string bossError;
        float tickAccumulator;

        bool rewinding;
        int autoRewindRemaining;
        float rewindSoundTimer;
        string message;
        float messageTimer;

        bool autoplay;
        string captureDirectory;
        int framesSinceCapture = -1;
        float autoRewindAt = -1f;
        float firstBossHpRatio = 1f;
        bool autoRewindDone;
        int rewindFrames;
        int framesSinceFirstHit = -1;

        // HUD に出す計測値（指数移動平均）
        float bossStepMs, engineStepMs, frameMs;

        bool BossCanRewind => bossError == null && boss.RewindUnsupportedReason == null;
        bool InDemo => autoplay || captureDirectory != null || autoRewindAt >= 0f;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            ParseArguments();
            SetUpCamera();
            sfx = new Sfx(gameObject);
            bulletRenderer = new CircleRenderer(BulletWorld.Capacity, BulletRadius);
            shotRenderer = new CircleRenderer(player.Shots.Length, 0.06f);
            playerRenderer = new CircleRenderer(1, Player.Radius);
            bossRenderer = new CircleRenderer(1, BossRadius);
            sparkRenderer = new CircleRenderer(effects.Sparks.Length, 0.05f);
            flashRenderer = new CircleRenderer(1, 0.9f);
            api = new BossApi(world);
            BossApi.Current = api;

            yield return PythonHome.Prepare();
            StartBoss(bossIndex);
            ready = true;
        }

        void ParseArguments()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoplay") autoplay = true;
                else if (args[i] == "-boss" && i + 1 < args.Length) bossIndex = int.Parse(args[i + 1]) % BossFactories.Length;
                else if (args[i] == "-capture" && i + 1 < args.Length) captureDirectory = args[i + 1];
                else if (args[i] == "-rewind-at" && i + 1 < args.Length) autoRewindAt = float.Parse(args[i + 1]);
                else if (args[i] == "-capture-at" && i + 1 < args.Length) captureAtSeconds = float.Parse(args[i + 1]);
                else if (args[i] == "-boss-hp" && i + 1 < args.Length) firstBossHpRatio = Mathf.Clamp01(float.Parse(args[i + 1]));
            }
        }

        void SetUpCamera()
        {
            mainCamera = Camera.main;
            mainCamera.orthographic = true;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Background;
            if (mainCamera.GetComponent<AudioListener>() == null)
                mainCamera.gameObject.AddComponent<AudioListener>();
        }

        // ---- ボスの切り替え ----

        void StartBoss(int index)
        {
            boss?.Dispose();
            bossIndex = index;
            bossHp = BossMaxHp * firstBossHpRatio;
            firstBossHpRatio = 1f;
            bossTime = 0f;
            framesSinceCapture = -1;
            autoRewindDone = false;
            bossError = null;
            rewinding = false;
            world.Clear();
            history.Clear();
            boss = BossFactories[index]();
            try
            {
                boss.Begin(api);
                var rewind = boss.RewindUnsupportedReason == null ? "rewindable" : "not rewindable";
                Debug.Log($"[BossRush] boss {index + 1}/{BossFactories.Length}: {boss.Language} \"{boss.Title}\" ({rewind})");
            }
            catch (Exception e)
            {
                FailBoss(e);
            }
        }

        // そのプラットフォームで動かない言語があっても、ゲームは止めずに次のボスへ進む
        void FailBoss(Exception e)
        {
            bossError = e.Message;
            Debug.LogError($"[BossRush] {boss.Language} boss failed: {e}");
            bossHp = 0f;
        }

        void NextBoss()
        {
            if (captureDirectory != null && bossIndex == BossFactories.Length - 1)
                Application.Quit();
            StartBoss((bossIndex + 1) % BossFactories.Length);
        }

        void ResetGame()
        {
            player.Reset();
            playerHits = 0;
            StartBoss(0);
            Debug.Log("[BossRush] reset");
        }

        // ---- 毎フレーム ----

        void Update()
        {
            if (!ready)
                return;
            var frameWatch = Stopwatch.StartNew();
            controls.Update();
            if (InDemo && controls.AnyInput)
                StopDemo();
            if (controls.ResetPressed)
                ResetGame();

            var bounds = UpdateCamera();
            var wantsRewind = controls.RewindHeld || autoRewindRemaining > 0;
            if (wantsRewind && !BossCanRewind && controls.RewindStarted)
                ShowMessage(bossError ?? boss.RewindUnsupportedReason);

            if (wantsRewind && BossCanRewind && history.Count > 0)
            {
                RewindOneStep();
            }
            else
            {
                if (rewinding)
                    EndRewind();
                autoRewindRemaining = 0;
                tickAccumulator = Mathf.Min(tickAccumulator + Time.deltaTime, TickSeconds * MaxTicksPerFrame);
                while (tickAccumulator >= TickSeconds)
                {
                    tickAccumulator -= TickSeconds;
                    Tick(bounds);
                }
            }

            effects.Update(Time.deltaTime);
            messageTimer -= Time.deltaTime;
            Draw();
            CaptureIfRequested();
            frameMs = Smooth(frameMs, (float)frameWatch.Elapsed.TotalMilliseconds);
        }

        Rect UpdateCamera()
        {
            mainCamera.orthographicSize = Mathf.Max(MinVisibleHalfHeight, MinVisibleHalfWidth / mainCamera.aspect);
            var shake = effects.ShakeOffset;
            mainCamera.transform.position = new Vector3(shake.x, shake.y, -10f);
            mainCamera.backgroundColor = rewinding ? RewindBackground : Background;
            var halfHeight = mainCamera.orthographicSize;
            var halfWidth = halfHeight * mainCamera.aspect;
            return new Rect(-halfWidth, -halfHeight, halfWidth * 2f, halfHeight * 2f);
        }

        void Tick(Rect bounds)
        {
            tickCount++;
            bossTime += TickSeconds;
            invincibleTimer -= TickSeconds;
            api.BossPosition = BossPositionAt(bossTime);
            api.PlayerPosition = player.Position;
            player.Update(TickSeconds, bounds, autoplay, controls.Direction);
            bossHp -= player.CollectHits(api.BossPosition, BossRadius);
            api.HpRatio = Mathf.Clamp01(bossHp / BossMaxHp);
            StepBoss();
            HandlePlayerHits(StepEngine(TickSeconds, bounds));

            if (BossCanRewind && tickCount % TicksPerSnapshot == 0)
                SaveSnapshot();
            if (autoplay && autoRewindAt >= 0f && !autoRewindDone && bossTime >= autoRewindAt)
            {
                autoRewindDone = true;
                autoRewindRemaining = AutoRewindSnapshots;
                if (!BossCanRewind)
                    ShowMessage(bossError ?? boss.RewindUnsupportedReason);
            }
            if (bossHp <= 0f && framesSinceCapture < 0)
                NextBoss();
        }

        static Vector2 BossPositionAt(float time) => new Vector2(Mathf.Sin(time * 0.5f) * 1.2f, 3f);

        void StepBoss()
        {
            if (bossError != null)
                return;
            var watch = Stopwatch.StartNew();
            try
            {
                // 撃ち終えたボスは最初からやり直させる
                if (boss.Step())
                {
                    boss.Dispose();
                    boss = BossFactories[bossIndex]();
                    boss.Begin(api);
                }
            }
            catch (Exception e)
            {
                FailBoss(e);
            }
            bossStepMs = Smooth(bossStepMs, (float)watch.Elapsed.TotalMilliseconds);
        }

        int StepEngine(float dt, Rect bounds)
        {
            var wellCount = bossError == null ? boss.GetWells(wells) : 0;
            for (var i = 0; i < wellCount; i++)
            {
                wells[i].X += api.BossPosition.x;
                wells[i].Y += api.BossPosition.y;
            }
            var arena = new Arena
            {
                MinX = bounds.xMin - 1f, MinY = bounds.yMin - 1f, MaxX = bounds.xMax + 1f, MaxY = bounds.yMax + 1f,
                PlayerX = player.Position.x, PlayerY = player.Position.y,
                HitRadius = Player.Radius * 0.5f + BulletRadius,
            };
            var watch = Stopwatch.StartNew();
            var hits = world.Step(wells, wellCount, arena, dt);
            engineStepMs = Smooth(engineStepMs, (float)watch.Elapsed.TotalMilliseconds);
            return hits;
        }

        void HandlePlayerHits(int hits)
        {
            if (hits == 0 || invincibleTimer > 0f)
                return;
            playerHits++;
            invincibleTimer = InvincibleSeconds;
            sfx.PlayHit();
            effects.Burst(player.Position);
            if (framesSinceFirstHit < 0)
                framesSinceFirstHit = 0;
        }

        // ---- 時間の巻き戻し ----

        void SaveSnapshot()
        {
            var snapshot = history.Push();
            world.SaveTo(snapshot);
            player.SaveTo(snapshot);
            snapshot.BossTime = bossTime;
            snapshot.BossHp = bossHp;
            snapshot.BossState = boss.SaveState(snapshot.BossState);
        }

        // 1 フレームに 1 つ（= 2 tick 分）ずつさかのぼるので、巻き戻しは 2 倍速に見える
        void RewindOneStep()
        {
            if (!rewinding)
            {
                rewinding = true;
                rewindFrames = 0;
                rewindSoundTimer = 0f;
                Debug.Log($"[BossRush] rewind start: {boss.Language} t={bossTime:F2}s, {history.Count} snapshots");
            }
            rewindFrames++;
            rewindSoundTimer -= Time.deltaTime;
            if (rewindSoundTimer <= 0f)
            {
                sfx.PlayRewind();
                rewindSoundTimer = RewindSoundInterval;
            }
            if (autoRewindRemaining > 0)
                autoRewindRemaining--;

            history.TryPop(out var snapshot);
            world.LoadFrom(snapshot);
            player.LoadFrom(snapshot);
            bossTime = snapshot.BossTime;
            bossHp = snapshot.BossHp;
            boss.LoadState(snapshot.BossState);
            api.BossPosition = BossPositionAt(bossTime);
            api.HpRatio = Mathf.Clamp01(bossHp / BossMaxHp);
            tickAccumulator = 0f;
        }

        void EndRewind()
        {
            rewinding = false;
            invincibleTimer = InvincibleSeconds;
            Debug.Log($"[BossRush] rewind end: t={bossTime:F2}s");
        }

        // Android は「最近使ったアプリ」から開き直すと、撮影用の引数が付いた Intent を再送することがある。
        // そうして始まったデモでも、触れば遊べるようにする
        void StopDemo()
        {
            autoplay = false;
            captureDirectory = null;
            autoRewindAt = -1f;
            autoRewindRemaining = 0;
            Debug.Log("[BossRush] demo stopped: manual play");
        }

        void ShowMessage(string text)
        {
            message = text;
            messageTimer = 3f;
            Debug.Log($"[BossRush] {text}");
        }

        // ---- 描画 ----

        void Draw()
        {
            bulletRenderer.Draw(world.Bullets, world.Count);
            shotRenderer.Draw(player.Shots, player.ShotCount);
            sparkRenderer.Draw(effects.Sparks, effects.SparkCount);
            if (effects.FlashVisible)
                flashRenderer.Draw(effects.Flash, 1);
            // 無敵のあいだは点滅させる
            var blink = invincibleTimer > 0f && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f;
            single[0] = new Bullet { X = player.Position.x, Y = player.Position.y, Rgba = blink ? 0xFFFFFF40u : 0xFFFFFFFFu };
            playerRenderer.Draw(single, 1);
            var hpColor = Color.Lerp(new Color(1f, 0.2f, 0.3f), new Color(0.6f, 0.4f, 1f), api.HpRatio);
            single[0] = new Bullet { X = api.BossPosition.x, Y = api.BossPosition.y, Rgba = BossApi.Rgba(hpColor) };
            bossRenderer.Draw(single, 1);
        }

        // 画面の保存はフレームの終わりに行われるので、撮ってから数フレーム待って次のボスへ進む
        void CaptureIfRequested()
        {
            if (captureDirectory == null)
                return;
            if (rewinding && rewindFrames == 20)
                Capture("rewind");
            // 最初に被弾した直後の画面も 1 枚だけ撮る（火花と閃光の確認用）
            if (framesSinceFirstHit >= 0 && framesSinceFirstHit++ == 4)
                Capture("hit");
            if (bossTime < captureAtSeconds || rewinding)
                return;
            if (framesSinceCapture < 0)
                Capture(null);
            if (++framesSinceCapture == 3)
                NextBoss();
        }

        void Capture(string suffix)
        {
            var name = $"{bossIndex + 1}-{boss.Language.Replace("#", "sharp")}{(suffix == null ? "" : "-" + suffix)}.png";
            var path = Path.Combine(captureDirectory, name);
#if UNITY_ANDROID && !UNITY_EDITOR
            // モバイルでは CaptureScreenshot がパスの前に persistentDataPath を付けるので、相対パスで渡す
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, captureDirectory));
#else
            Directory.CreateDirectory(captureDirectory);
#endif
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[BossRush] captured {path} bullets={world.Count} boss={bossStepMs:F3}ms engine={engineStepMs:F3}ms");
        }

        static float Smooth(float average, float sample) => average == 0f ? sample : Mathf.Lerp(average, sample, 0.05f);

        void OnGUI()
        {
            if (!ready)
                return;
            var scale = Controls.GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };
            var small = new GUIStyle(style) { fontSize = 16, wordWrap = true };

            GUI.Label(new Rect(16, 12, width - 32, 32), $"<b>BOSS {bossIndex + 1}/{BossFactories.Length}  {boss.Language}</b>  {boss.Title}", style);
            GUI.Box(new Rect(16, 46, (width - 32) * api.HpRatio, 10), GUIContent.none);
            var stats = $"bullets {world.Count,6}   hits {playerHits}\n" +
                        $"boss step {bossStepMs:F3} ms   engine {engineStepMs:F3} ms ({world.ThreadCount} threads)\n" +
                        $"frame {frameMs:F2} ms   fps {1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):F0}";
            GUI.Label(new Rect(16, 62, width - 32, 70), stats, small);
            var rewindStatus = BossCanRewind
                ? $"<color=#88ffcc>rewind: {history.Count * TicksPerSnapshot / 60f:F1}s buffered</color>"
                : $"<color=#999999>rewind: unavailable</color>";
            GUI.Label(new Rect(16, 130, width - 32, 24), rewindStatus, small);
            if (bossError != null)
                GUI.Label(new Rect(16, 156, width - 32, 120), "<color=#ff6677>" + bossError + "</color>", small);
            if (messageTimer > 0f && message != null)
                GUI.Label(new Rect(16, 156, width - 32, 120), "<color=#ffcc66>" + message + "</color>", small);
            var big = new GUIStyle(style) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
            if (rewinding)
                GUI.Label(new Rect(0, 360, width, 80), "<b><color=#ffcc66><< REWIND</color></b>", big);
            else if (InDemo)
                GUI.Label(new Rect(0, 330, width, 140), "<b><color=#88ffcc>DEMO</color></b>\n<size=22>touch to play</size>", big);
            controls.Draw(BossCanRewind);
        }

        void OnDestroy()
        {
            boss?.Dispose();
            bulletRenderer?.Dispose();
            shotRenderer?.Dispose();
            playerRenderer?.Dispose();
            bossRenderer?.Dispose();
            sparkRenderer?.Dispose();
            flashRenderer?.Dispose();
        }
    }
}
