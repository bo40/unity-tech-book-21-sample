// CSS の色文字列を型で縛る。形式が違えばコンパイルエラーになる
export type Hex = `#${string}`;
export type Hsl = `hsl(${number} ${number}% ${number}%)`;
export type Color = Hex | Hsl;

// 0xRRGGBBAA に変換する
export function toRgba(color: Color): number {
  if (color.startsWith("#")) {
    const hex = color.slice(1);
    const full = hex.length === 3 ? [...hex].map(c => c + c).join("") : hex;
    return (parseInt(full.padEnd(8, "f"), 16)) >>> 0;
  }
  const [h, s, l] = color.match(/[\d.]+/g)!.map(Number);
  const [r, g, b] = hslToRgb(h / 360, s / 100, l / 100);
  return ((r << 24) | (g << 16) | (b << 8) | 0xff) >>> 0;
}

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
  const p = 2 * l - q;
  const channel = (t: number) => {
    t = (t + 1) % 1;
    const v = t < 1 / 6 ? p + (q - p) * 6 * t : t < 1 / 2 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p;
    return Math.round(v * 255);
  };
  return [channel(h + 1 / 3), channel(h), channel(h - 1 / 3)];
}
