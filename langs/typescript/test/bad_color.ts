// 型エラーになることを確かめるためのファイル（ビルドには含めない）
import type { Color } from "../src/color";

export const ok: Color = "hsl(340 95% 60%)";
export const commaSeparated: Color = "hsl(340, 95%, 60%)";
export const noPercent: Color = "hsl(340 95 60)";
export const named: Color = "red";
