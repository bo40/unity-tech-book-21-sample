import { type Color, toRgba } from "./color";

export interface Vec2 { x: number; y: number }

export interface BulletSpec {
  /** ボスからの相対位置 */
  from?: Vec2;
  angle: number;
  /** 毎秒のワールド単位 */
  speed: number;
  /** 毎秒の速さの増減。負なら減速して止まる */
  accel?: number;
  color: Color;
}

export interface BossApi {
  hpRatio(): number;
  aimAngle(): number;
  /** 弾を出し、止まる位置を返す（accel < 0 のとき） */
  spawn(spec: BulletSpec): { stopsAt: Vec2 };
  /** n フレーム後に resolve する Promise */
  frames(n: number): Promise<void>;
}

// 待っている Promise の resolve と残りフレーム数
const waiting: { remaining: number; resolve: () => void }[] = [];

/** ホストが毎フレーム 1 回呼ぶ。期限の来た Promise を resolve する（続きはジョブキューで走る） */
export function tick(): void {
  for (let i = waiting.length - 1; i >= 0; i--) {
    if (--waiting[i].remaining <= 0) {
      waiting[i].resolve();
      waiting.splice(i, 1);
    }
  }
}

export const api: BossApi = {
  hpRatio: () => __unihub_hp_ratio(),
  aimAngle: () => __unihub_aim_angle(),
  spawn({ from = { x: 0, y: 0 }, angle, speed, accel = 0, color }) {
    __unihub_spawn(from.x, from.y, angle, speed, accel, toRgba(color));
    // 等加速度運動で止まるまでに進む距離 v² / 2|a|
    const distance = accel < 0 ? (speed * speed) / (2 * -accel) : 0;
    return { stopsAt: { x: from.x + Math.cos(angle) * distance, y: from.y + Math.sin(angle) * distance } };
  },
  frames: n => new Promise(resolve => waiting.push({ remaining: n, resolve })),
};
