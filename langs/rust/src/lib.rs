//! Unity から呼ばれる多言語ハブ。
//! - 弾幕エンジン（danmaku）と、ボス 2 の重力井戸（gravity_boss）
//! - ボス 4 の TypeScript を動かす QuickJS（js）
//! - ボス 6 の OCaml を動かす wasmtime（wasm）
//!
//! ボスの生成関数はどれも UniBridge（langs/common/unibridge.h）を受け取り、
//! step は 0: 継続中、1: 終了、-1: エラー（理由は unihub_last_error）を返す。

pub mod bridge;
pub mod danmaku;
pub mod gravity_boss;
pub mod js;
pub mod wasm;

use std::cell::RefCell;
use std::panic::{AssertUnwindSafe, catch_unwind};

use bridge::UniBridge;
use danmaku::Well;
use gravity_boss::GravityBoss;
use js::JsBoss;
#[cfg(feature = "jit")]
pub use wasm::precompile;
use wasm::WasmBoss;

thread_local! {
    static LAST_ERROR: RefCell<String> = const { RefCell::new(String::new()) };
}

fn set_last_error(message: String) {
    LAST_ERROR.with(|cell| *cell.borrow_mut() = message);
}

/// Rust のパニックを C ABI の外に漏らさず、エラーとして記録する
fn guard<T>(name: &str, fallback: T, body: impl FnOnce() -> Result<T, String>) -> T {
    match catch_unwind(AssertUnwindSafe(body)) {
        Ok(Ok(value)) => value,
        Ok(Err(error)) => {
            set_last_error(error);
            fallback
        }
        Err(_) => {
            set_last_error(format!("panic in {name}"));
            fallback
        }
    }
}

fn into_raw<T>(value: T) -> *mut T {
    Box::into_raw(Box::new(value))
}

fn step_status(result: Result<bool, String>) -> Result<i32, String> {
    result.map(|finished| finished as i32)
}

/// 直近のエラーを UTF-8 で buf に書き、書いたバイト数を返す
#[unsafe(no_mangle)]
pub extern "C" fn unihub_last_error(buf: *mut u8, capacity: usize) -> usize {
    LAST_ERROR.with(|cell| {
        let message = cell.borrow();
        let length = message.len().min(capacity);
        unsafe { std::ptr::copy_nonoverlapping(message.as_ptr(), buf, length) };
        length
    })
}

// ---- ボス 3: Rust（重力井戸） ----

#[unsafe(no_mangle)]
pub extern "C" fn rust_boss_new(bridge: UniBridge) -> *mut GravityBoss {
    into_raw(GravityBoss::new(bridge))
}

#[unsafe(no_mangle)]
pub extern "C" fn rust_boss_step(boss: *mut GravityBoss) -> i32 {
    guard("rust_boss_step", -1, || {
        unsafe { &mut *boss }.step();
        Ok(0)
    })
}

/// いまの井戸の配置を out に書き、書いた数を返す
#[unsafe(no_mangle)]
pub extern "C" fn rust_boss_wells(boss: *const GravityBoss, out: *mut Well, capacity: usize) -> usize {
    let wells = unsafe { &*boss }.wells();
    let count = wells.len().min(capacity);
    unsafe { std::ptr::copy_nonoverlapping(wells.as_ptr(), out, count) };
    count
}

/// 時間の巻き戻し用。状態（フレーム数）を読み書きする
#[unsafe(no_mangle)]
pub extern "C" fn rust_boss_frame(boss: *const GravityBoss) -> u32 {
    unsafe { &*boss }.frame()
}

#[unsafe(no_mangle)]
pub extern "C" fn rust_boss_set_frame(boss: *mut GravityBoss, frame: u32) {
    unsafe { &mut *boss }.set_frame(frame)
}

#[unsafe(no_mangle)]
pub extern "C" fn rust_boss_free(boss: *mut GravityBoss) {
    if !boss.is_null() {
        drop(unsafe { Box::from_raw(boss) });
    }
}

// ---- ボス 4: TypeScript（QuickJS） ----

/// source は UTF-8 の JS。失敗したら null
#[unsafe(no_mangle)]
pub extern "C" fn js_boss_new(source: *const u8, length: usize, bridge: UniBridge) -> *mut JsBoss {
    guard("js_boss_new", std::ptr::null_mut(), || {
        let source = unsafe { std::slice::from_raw_parts(source, length) };
        let source = std::str::from_utf8(source).map_err(|error| error.to_string())?;
        JsBoss::new(source, bridge).map(into_raw)
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn js_boss_step(boss: *mut JsBoss) -> i32 {
    guard("js_boss_step", -1, || step_status(unsafe { &mut *boss }.step()))
}

#[unsafe(no_mangle)]
pub extern "C" fn js_boss_free(boss: *mut JsBoss) {
    if !boss.is_null() {
        drop(unsafe { Box::from_raw(boss) });
    }
}

// ---- ボス 6: OCaml（wasmtime） ----

/// bytes は .cwasm（事前コンパイル済み）か .wasm（JIT 版のビルドのみ）。失敗したら null
#[unsafe(no_mangle)]
pub extern "C" fn wasm_boss_new(bytes: *const u8, length: usize, bridge: UniBridge) -> *mut WasmBoss {
    guard("wasm_boss_new", std::ptr::null_mut(), || {
        let bytes = unsafe { std::slice::from_raw_parts(bytes, length) };
        WasmBoss::new(bytes, bridge).map(into_raw).map_err(|error| format!("{error:?}"))
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn wasm_boss_step(boss: *mut WasmBoss) -> i32 {
    guard("wasm_boss_step", -1, || step_status(unsafe { &mut *boss }.step().map_err(|error| format!("{error:?}"))))
}

#[unsafe(no_mangle)]
pub extern "C" fn wasm_boss_free(boss: *mut WasmBoss) {
    if !boss.is_null() {
        drop(unsafe { Box::from_raw(boss) });
    }
}
