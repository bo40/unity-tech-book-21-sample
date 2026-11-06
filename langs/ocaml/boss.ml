(* ボス 6: OCaml ─ フィボナッチのひまわり。
   第 n 波の弾数をフィボナッチ数にし、弾の角度を黄金角ずつずらす。
   wasm_of_ocaml で WASI モジュールにし、Rust の unihub（wasmtime）が動かす。 *)

(* ホストの関数。実体は unihub.wat を経由して Rust に届く *)
external host_spawn : float -> float -> int -> unit = "unihub_spawn"
external frame_end : unit -> unit = "unihub_frame_end"

(* ---- 「待つ」を effect で表す ---- *)

type _ Effect.t += Wait : int -> unit Effect.t
let wait n = Effect.perform (Wait n)

(* 型注釈がないと「型がスコープの外に漏れる」エラーになる（GADT のせい） *)
let pending : (int * (unit, unit) Effect.Deep.continuation) option ref = ref None

let start boss =                         (* OCaml 5.3 の effect パターン構文 *)
  match boss () with
  | () -> pending := None
  | effect (Wait n), k -> pending := Some (n, k)

let step () =
  match !pending with
  | Some (0, k) -> Effect.Deep.continue k ()
  | Some (n, k) -> pending := Some (n - 1, k)
  | None -> ()

(* ---- ボス本体 ---- *)

(* おじさんの一行: 定義をそのまま書く *)
let rec fib = function 0 | 1 -> 1 | n -> fib (n - 1) + fib (n - 2)

(* 実際に使うのは無限列。必要なぶんだけ遅延評価で取り出す *)
let fibs = Seq.unfold (fun (a, b) -> Some (a, (b, a + b))) (1, 1)

let golden_angle = Float.pi *. (3. -. sqrt 5.)   (* 約 137.5° *)

(* 色相 h（0〜1）を彩度・明度 1 の RGB にする *)
let rgb_of_hue h =
  let channel offset =
    let k = Float.rem ((h *. 6.) +. offset) 6. in
    let v = 1. -. Float.max 0. (Float.min 1. (Float.min k (4. -. k))) in
    int_of_float (v *. 255.)
  in
  (channel 5. lsl 16) lor (channel 3. lsl 8) lor channel 1.

let sunflower () =
  fibs |> Seq.take 10 |> Seq.iteri (fun wave count ->
    for i = 0 to count - 1 do
      let angle = golden_angle *. float (i + wave * 100) in
      host_spawn angle (2.0 +. 0.25 *. float wave)
        (rgb_of_hue (Float.rem angle (2. *. Float.pi) /. (2. *. Float.pi)))
    done;
    wait 40)

(* ボスの待ち（OCaml の継続）を 1 フレームずつ進め、フレームの切れ目では Wasm ごと止まる *)
let () =
  assert (fib 10 = 89);
  start sunflower;
  while Option.is_some !pending do
    step ();
    frame_end ()
  done
