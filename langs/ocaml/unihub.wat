;; OCaml（wasm_of_ocaml）とホスト（Rust の unihub）をつなぐ接着コード。
;; OCaml の値は GC 参照 (ref eq) なので、ここで f64 や i32 に剥がしてからホストに渡す
(module
   (import "unihub" "spawn" (func $host_spawn (param f64 f64 i32)))
   (import "unihub" "frame_end" (func $host_frame_end))

   (type $float (struct (field $f f64)))

   ;; float は箱詰めされた構造体、int は 31 ビットの i31ref
   (func (export "unihub_spawn")
      (param $angle (ref eq)) (param $speed (ref eq)) (param $rgb (ref eq)) (result (ref eq))
      (call $host_spawn
         (struct.get $float $f (ref.cast (ref $float) (local.get $angle)))
         (struct.get $float $f (ref.cast (ref $float) (local.get $speed)))
         (i31.get_s (ref.cast (ref i31) (local.get $rgb))))
      (ref.i31 (i32.const 0)))

   ;; ホスト側でこの呼び出しごと Wasm の実行を一時停止し、次のフレームで再開する
   (func (export "unihub_frame_end") (param (ref eq)) (result (ref eq))
      (call $host_frame_end)
      (ref.i31 (i32.const 0)))
)
