//! ボス 6: OCaml（wasm_of_ocaml → WASI）と、ボス 7: Go（GOOS=wasip1）を wasmtime で動かす。
//! どちらも同じ受け口（`unihub` モジュールの import）を使う。
//!
//! ボス側は毎フレーム `unihub.frame_end` を呼ぶ。ホストはこれを async 関数にして 1 回だけ Pending を返す。
//! wasmtime は async 呼び出しを専用スタック（fiber）上で実行しているので、Wasm の実行はその場で凍結され、
//! `step` は Unity に戻る。次の `step` で同じ場所から再開する。
//! ボスの「待ち」は OCaml の継続（effect handler）、フレームの切れ目は wasmtime の fiber という二段構え。

use std::future::Future;
use std::pin::Pin;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::task::{Context, Poll, Waker};

use wasmtime::{Caller, Config, Engine, Linker, Module, Store};
use wasmtime_wasi::WasiCtxBuilder;
use wasmtime_wasi::p1::{self, WasiP1Ctx};

use crate::bridge::UniBridge;

struct HostState {
    wasi: WasiP1Ctx,
    bridge: UniBridge,
}

type BossFuture = Pin<Box<dyn Future<Output = wasmtime::Result<()>> + Send>>;

pub struct WasmBoss {
    future: BossFuture,
    // frame_end が呼ばれたら立つ。WASI の I/O など、ほかの理由の Pending と区別するため
    reached_frame_end: Arc<AtomicBool>,
    finished: bool,
}

/// 最初の poll で Pending、2 回目で Ready になるだけの Future
struct YieldOnce(bool);

impl Future for YieldOnce {
    type Output = ();
    fn poll(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<()> {
        if self.0 {
            Poll::Ready(())
        } else {
            self.0 = true;
            cx.waker().wake_by_ref();
            Poll::Pending
        }
    }
}

fn wasm_config() -> Config {
    let mut config = Config::new();
    // wasm_of_ocaml の出力に必要な拡張
    config.wasm_gc(true);
    config.wasm_function_references(true);
    config.wasm_exceptions(true);
    config.wasm_tail_call(true);
    config
}

/// .wasm ならその場で JIT コンパイルし、.cwasm（事前コンパイル済み）ならそのまま読み込む
fn load_module(engine: &Engine, bytes: &[u8]) -> wasmtime::Result<Module> {
    if bytes.starts_with(b"\0asm") {
        compile_module(engine, bytes)
    } else {
        // .cwasm の中身は機械語そのもの。自分で作ってアプリに同梱したものだけを渡すこと
        unsafe { Module::deserialize(engine, bytes) }
    }
}

#[cfg(feature = "jit")]
fn compile_module(engine: &Engine, wasm: &[u8]) -> wasmtime::Result<Module> {
    Module::new(engine, wasm)
}

#[cfg(not(feature = "jit"))]
fn compile_module(_engine: &Engine, _wasm: &[u8]) -> wasmtime::Result<Module> {
    wasmtime::bail!("this build has no JIT compiler; pass a precompiled .cwasm instead")
}

/// target（例: aarch64-linux-android）向けに .wasm を事前コンパイルして .cwasm のバイト列を返す
#[cfg(feature = "jit")]
pub fn precompile(wasm: &[u8], target: &str) -> wasmtime::Result<Vec<u8>> {
    let mut config = wasm_config();
    config.target(target)?;
    Engine::new(&config)?.precompile_module(wasm)
}

impl WasmBoss {
    pub fn new(bytes: &[u8], bridge: UniBridge) -> wasmtime::Result<Self> {
        let engine = Engine::new(&wasm_config())?;
        let module = load_module(&engine, bytes)?;

        let mut linker: Linker<HostState> = Linker::new(&engine);
        p1::add_to_linker_async(&mut linker, |state| &mut state.wasi)?;
        linker.func_wrap(
            "unihub",
            "spawn",
            |caller: Caller<'_, HostState>, angle: f64, speed: f64, rgb: i32| {
                caller.data().bridge.spawn(0.0, 0.0, angle as f32, speed as f32, 0.0, (rgb as u32) << 8 | 0xFF);
            },
        )?;
        // Go のボスが使う。ボスから見た (x, y) から撃つ版と、残り HP
        linker.func_wrap(
            "unihub",
            "spawn_at",
            |caller: Caller<'_, HostState>, x: f64, y: f64, angle: f64, speed: f64, rgb: i32| {
                let rgba = (rgb as u32) << 8 | 0xFF;
                caller.data().bridge.spawn(x as f32, y as f32, angle as f32, speed as f32, 0.0, rgba);
            },
        )?;
        linker.func_wrap("unihub", "hp", |caller: Caller<'_, HostState>| caller.data().bridge.hp_ratio() as f64)?;
        let reached_frame_end = Arc::new(AtomicBool::new(false));
        let flag = reached_frame_end.clone();
        linker.func_wrap_async("unihub", "frame_end", move |_caller: Caller<'_, HostState>, (): ()| {
            flag.store(true, Ordering::Relaxed);
            Box::new(YieldOnce(false))
        })?;

        let wasi = WasiCtxBuilder::new().build_p1();
        let mut store = Store::new(&engine, HostState { wasi, bridge });
        let future: BossFuture = Box::pin(async move {
            let instance = linker.instantiate_async(&mut store, &module).await?;
            let start = instance.get_typed_func::<(), ()>(&mut store, "_start")?;
            match start.call_async(&mut store, ()).await {
                Ok(()) => Ok(()),
                // OCaml のプログラムが正常終了すると WASI の proc_exit(0) が飛んでくる
                Err(error) => match error.downcast_ref::<wasmtime_wasi::I32Exit>() {
                    Some(exit) if exit.0 == 0 => Ok(()),
                    _ => Err(error),
                },
            }
        });
        Ok(Self { future, reached_frame_end, finished: false })
    }

    /// 1 フレーム進める。終了したら Ok(true)
    pub fn step(&mut self) -> wasmtime::Result<bool> {
        if self.finished {
            return Ok(true);
        }
        let mut context = Context::from_waker(Waker::noop());
        // frame_end に着くか終わるまで回す。途中の Pending（WASI の I/O など）はフレームの切れ目ではない
        loop {
            match self.future.as_mut().poll(&mut context) {
                Poll::Pending => {
                    if self.reached_frame_end.swap(false, Ordering::Relaxed) {
                        return Ok(false);
                    }
                }
                Poll::Ready(result) => {
                    self.finished = true;
                    return result.map(|()| true);
                }
            }
        }
    }
}
