// ボス 4: TypeScript ─ 色は CSS で書かせろ。
// 打ち上げて減速し、止まった瞬間に全方位へ散る花火
import type { Color } from "./color";
import type { BossApi, Vec2 } from "./runtime";

const phases = [
  { above: 0.5, shells: 3, sparks: 24, color: "hsl(200 90% 60%)" },
  { above: 0.0, shells: 5, sparks: 40, color: "hsl(340 95% 60%)" },
] as const satisfies readonly { above: number; shells: number; sparks: number; color: Color }[];

function burst(api: BossApi, at: Vec2, sparks: number, color: Color) {
  for (let i = 0; i < sparks; i++)
    api.spawn({ from: at, angle: (2 * Math.PI * i) / sparks, speed: 2, color });
}

export default async function boss(api: BossApi) {
  while (true) {
    const phase = phases.find(p => api.hpRatio() > p.above)!;
    for (let s = 0; s < phase.shells; s++) {
      const angle = -Math.PI / 2 + (s - (phase.shells - 1) / 2) * 0.35;   // 自機のほうへ扇状に撃ち出す
      const shell = api.spawn({ angle, speed: 6, accel: -12, color: phase.color });
      await api.frames(30);                       // 打ち上げた弾が止まるまで待つ
      burst(api, shell.stopsAt, phase.sparks, phase.color);
    }
    await api.frames(40);
  }
}
