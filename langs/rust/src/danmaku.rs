//! 弾幕エンジン。重力井戸に引き寄せられる大量の弾を毎フレーム更新し、
//! 画面外に出た弾と自機に当たった弾を取り除く。
//! 弾の配列は C# 側が持っていて、ポインタだけを受け取る（コピーしない）。

use rayon::prelude::*;

/// C# の struct Bullet と同じ並び（24 バイト）
#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub struct Bullet {
    pub x: f32,
    pub y: f32,
    pub vx: f32,
    pub vy: f32,
    /// 毎秒の速さの増減（進行方向）
    pub accel: f32,
    pub rgba: u32,
}

/// 重力井戸（ブラックホール）
#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub struct Well {
    pub x: f32,
    pub y: f32,
    pub strength: f32,
    pub padding: f32,
}

/// 弾が生きていられる範囲と、自機の当たり判定
#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct Arena {
    pub min_x: f32,
    pub min_y: f32,
    pub max_x: f32,
    pub max_y: f32,
    pub player_x: f32,
    pub player_y: f32,
    /// 自機と弾の半径の和
    pub hit_radius: f32,
    pub padding: f32,
}

/// 井戸の中心で力が無限大にならないようにする
const SOFTENING: f32 = 0.05;
/// 減速して止まった弾が消えるまでの速さ（アルファ値 / 秒）。約 1.5 秒で消える
const FADE_PER_SECOND: f32 = 170.0;

fn update_bullet(bullet: &mut Bullet, wells: &[Well], dt: f32) {
    let (mut ax, mut ay) = (0.0f32, 0.0f32);
    for well in wells {
        let dx = well.x - bullet.x;
        let dy = well.y - bullet.y;
        let r2 = dx * dx + dy * dy + SOFTENING;
        let pull = well.strength / (r2 * r2.sqrt());
        ax += dx * pull;
        ay += dy * pull;
    }
    if bullet.accel != 0.0 {
        let speed = (bullet.vx * bullet.vx + bullet.vy * bullet.vy).sqrt();
        if speed > 0.0 {
            let scale = (speed + bullet.accel * dt).max(0.0) / speed;
            bullet.vx *= scale;
            bullet.vy *= scale;
        } else if bullet.accel < 0.0 {
            // 減速して止まった弾は、アルファ値を減らして消していく（0 になったら sweep が取り除く）
            let alpha = bullet.rgba & 0xFF;
            let fade = ((FADE_PER_SECOND * dt) as u32).max(1);
            bullet.rgba = bullet.rgba & !0xFF | alpha.saturating_sub(fade);
        }
    }
    bullet.vx += ax * dt;
    bullet.vy += ay * dt;
    bullet.x += bullet.vx * dt;
    bullet.y += bullet.vy * dt;
}

pub fn update(bullets: &mut [Bullet], wells: &[Well], dt: f32, parallel: bool) {
    if parallel {
        // iter_mut を par_iter_mut に変えるだけで並列化できる。データ競合はコンパイラが検査済み
        bullets.par_iter_mut().for_each(|bullet| update_bullet(bullet, wells, dt));
    } else {
        bullets.iter_mut().for_each(|bullet| update_bullet(bullet, wells, dt));
    }
}

/// 画面外の弾と自機に当たった弾を、末尾の弾と入れ替えて消す。(残った数, 当たった数) を返す
pub fn sweep(bullets: &mut [Bullet], arena: &Arena) -> (usize, u32) {
    let mut count = bullets.len();
    let mut hits = 0;
    let mut i = 0;
    while i < count {
        let b = bullets[i];
        let dx = b.x - arena.player_x;
        let dy = b.y - arena.player_y;
        let hit = dx * dx + dy * dy < arena.hit_radius * arena.hit_radius;
        let outside = b.x < arena.min_x || b.x > arena.max_x || b.y < arena.min_y || b.y > arena.max_y;
        let faded = b.rgba & 0xFF == 0;
        if hit || outside || faded {
            hits += hit as u32;
            count -= 1;
            bullets[i] = bullets[count];
        } else {
            i += 1;
        }
    }
    (count, hits)
}

/// 1 フレーム分進める。戻り値は残った弾の数。当たった数は hits に書く
#[unsafe(no_mangle)]
pub extern "C" fn danmaku_step(
    bullets: *mut Bullet,
    bullet_count: usize,
    wells: *const Well,
    well_count: usize,
    arena: *const Arena,
    dt: f32,
    parallel: i32,
    hits: *mut u32,
) -> usize {
    // unsafe はここだけ。ここから先は安全な Rust
    let bullets = unsafe { std::slice::from_raw_parts_mut(bullets, bullet_count) };
    let wells = if well_count == 0 { &[][..] } else { unsafe { std::slice::from_raw_parts(wells, well_count) } };
    let arena = unsafe { &*arena };
    update(bullets, wells, dt, parallel != 0);
    let (count, hit_count) = sweep(bullets, arena);
    unsafe { *hits = hit_count };
    count
}

/// rayon のワーカースレッド数（画面表示用）
#[unsafe(no_mangle)]
pub extern "C" fn danmaku_thread_count() -> i32 {
    rayon::current_num_threads() as i32
}
