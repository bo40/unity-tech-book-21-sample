package main

// ホスト（Rust の unihub に組み込んだ wasmtime）の関数。OCaml のボスと同じ受け口を使う

// spawnAt はボスから見た (x, y) に弾を出す
//
//go:wasmimport unihub spawn_at
func spawnAt(x, y, angle, speed float64, rgb int32)

// hp はボスの残り HP の割合（1 → 0）
//
//go:wasmimport unihub hp
func hp() float64

// frameEnd を呼ぶと、ホストはこのフレームを終えて Wasm ごと止める。次のフレームで呼び出しから戻ってくる
//
//go:wasmimport unihub frame_end
func frameEnd()
