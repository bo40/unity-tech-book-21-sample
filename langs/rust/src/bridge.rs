//! langs/common/unibridge.h の Rust 版

use std::ffi::c_void;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct UniBridge {
    pub spawn: extern "C" fn(x: f32, y: f32, angle: f32, speed: f32, accel: f32, rgba: u32, user: *mut c_void),
    pub hp_ratio: extern "C" fn(user: *mut c_void) -> f32,
    pub aim_angle: extern "C" fn(user: *mut c_void) -> f32,
    pub user: *mut c_void,
}

// 中身は関数ポインタと Unity 側の不透明なポインタだけ。呼び出しはすべて Unity のメインスレッドから行う
unsafe impl Send for UniBridge {}
unsafe impl Sync for UniBridge {}

impl UniBridge {
    pub fn spawn(&self, x: f32, y: f32, angle: f32, speed: f32, accel: f32, rgba: u32) {
        (self.spawn)(x, y, angle, speed, accel, rgba, self.user)
    }

    pub fn hp_ratio(&self) -> f32 {
        (self.hp_ratio)(self.user)
    }

    pub fn aim_angle(&self) -> f32 {
        (self.aim_angle)(self.user)
    }
}
