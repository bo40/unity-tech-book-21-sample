//! ボス 4: TypeScript（esbuild で 1 ファイルの JS にしたもの）を QuickJS-ng で動かす。
//!
//! ボスは async/await で書かれている。`api.frames(n)` は JS 側で Promise を作って待ち行列に入れるだけ。
//! ホストは毎フレーム `__unihub_tick()` を呼んで期限の来た Promise を resolve し、
//! そのあと QuickJS のジョブキュー（Promise の続き）を空になるまで回す。
//! つまり `await` の正体は「ジョブキューに積まれたコールバック」。

use rquickjs::{CatchResultExt, Context, Function, Object, Runtime};

use crate::bridge::UniBridge;

pub struct JsBoss {
    runtime: Runtime,
    context: Context,
    finished: bool,
}

fn js_error(error: impl std::fmt::Display) -> String {
    error.to_string()
}

impl JsBoss {
    pub fn new(source: &str, bridge: UniBridge) -> Result<Self, String> {
        let runtime = Runtime::new().map_err(js_error)?;
        let context = Context::full(&runtime).map_err(js_error)?;
        context.with(|ctx| {
            let globals = ctx.globals();
            let spawn = Function::new(
                ctx.clone(),
                move |x: f64, y: f64, angle: f64, speed: f64, accel: f64, rgba: f64| {
                    bridge.spawn(x as f32, y as f32, angle as f32, speed as f32, accel as f32, rgba as u32);
                },
            )
            .catch(&ctx)
            .map_err(js_error)?;
            globals.set("__unihub_spawn", spawn).catch(&ctx).map_err(js_error)?;
            let hp = Function::new(ctx.clone(), move || bridge.hp_ratio() as f64).catch(&ctx).map_err(js_error)?;
            globals.set("__unihub_hp_ratio", hp).catch(&ctx).map_err(js_error)?;
            let aim = Function::new(ctx.clone(), move || bridge.aim_angle() as f64).catch(&ctx).map_err(js_error)?;
            globals.set("__unihub_aim_angle", aim).catch(&ctx).map_err(js_error)?;
            ctx.eval::<(), _>(source).catch(&ctx).map_err(js_error)
        })?;
        let mut boss = Self { runtime, context, finished: false };
        boss.drain_jobs()?;
        Ok(boss)
    }

    /// 1 フレーム進める。終了したら Ok(true)
    pub fn step(&mut self) -> Result<bool, String> {
        if self.finished {
            return Ok(true);
        }
        self.context.with(|ctx| {
            let tick: Function = ctx.globals().get("__unihub_tick").catch(&ctx).map_err(js_error)?;
            tick.call::<_, ()>(()).catch(&ctx).map_err(js_error)
        })?;
        self.drain_jobs()?;
        let (done, error) = self.context.with(|ctx| -> Result<(bool, String), String> {
            let state: Object = ctx.globals().get("__unihub_state").catch(&ctx).map_err(js_error)?;
            Ok((
                state.get("done").catch(&ctx).map_err(js_error)?,
                state.get("error").catch(&ctx).map_err(js_error)?,
            ))
        })?;
        if !error.is_empty() {
            return Err(error);
        }
        self.finished = done;
        Ok(done)
    }

    /// Promise の続き（マイクロタスク）を全部実行する
    fn drain_jobs(&mut self) -> Result<(), String> {
        loop {
            match self.runtime.execute_pending_job() {
                Ok(true) => continue,
                Ok(false) => return Ok(()),
                Err(error) => return Err(format!("{error:?}")),
            }
        }
    }
}
