//! ボス 3: Rust ─ 重力井戸。
//! 渦巻き状に弾を撒きつつ、ボスの周りを回る井戸で弾を引き寄せる。
//! 弾の動きそのものは弾幕エンジン（danmaku.rs）が計算する。

use std::f32::consts::TAU;

use crate::bridge::UniBridge;
use crate::danmaku::Well;

const ARMS: u32 = 6;
const FRAMES_PER_VOLLEY: u32 = 3;
const WELL_COUNT: usize = 3;
const WELL_ORBIT_RADIUS: f32 = 2.5;

pub struct GravityBoss {
    bridge: UniBridge,
    frame: u32,
}

impl GravityBoss {
    pub fn new(bridge: UniBridge) -> Self {
        Self { bridge, frame: 0 }
    }

    pub fn step(&mut self) {
        self.frame += 1;
        if self.frame % FRAMES_PER_VOLLEY != 0 {
            return;
        }
        let spin = self.frame as f32 * 0.05;
        // HP が減るほど速く撃つ
        let speed = 2.0 + (1.0 - self.bridge.hp_ratio()) * 2.0;
        for arm in 0..ARMS {
            let angle = spin + TAU * arm as f32 / ARMS as f32;
            self.bridge.spawn(0.0, 0.0, angle, speed, 0.0, heat_color(arm as f32 / ARMS as f32));
        }
    }

    /// ボスの状態はフレーム数だけ。普通の値なので、そのまま保存して戻せる
    pub fn frame(&self) -> u32 {
        self.frame
    }

    pub fn set_frame(&mut self, frame: u32) {
        self.frame = frame;
    }

    /// ボスの位置からの相対座標で、いまの井戸の配置を返す
    pub fn wells(&self) -> [Well; WELL_COUNT] {
        let t = self.frame as f32 / 60.0;
        std::array::from_fn(|i| {
            let angle = t * 0.7 + TAU * i as f32 / WELL_COUNT as f32;
            Well {
                x: angle.cos() * WELL_ORBIT_RADIUS,
                y: angle.sin() * WELL_ORBIT_RADIUS - 2.0,
                strength: 1.5,
                padding: 0.0,
            }
        })
    }
}

fn heat_color(t: f32) -> u32 {
    let red = (255.0 * t) as u32;
    let blue = (255.0 * (1.0 - t)) as u32;
    red << 24 | 0x80 << 16 | blue << 8 | 0xFF
}
