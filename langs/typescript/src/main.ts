// QuickJS に読み込ませるエントリポイント。esbuild で 1 ファイルの IIFE にする
import boss from "./boss";
import { api, tick } from "./runtime";

const state = { done: false, error: "" };

boss(api).then(
  () => { state.done = true; },
  (e: unknown) => { state.done = true; state.error = String(e); },
);

Object.assign(globalThis, {
  __unihub_tick: tick,
  __unihub_state: state,
});
