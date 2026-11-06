(* 型付き中間表現から LLVM IR（テキスト）を出す。
   ボスの中断状態は「フレーム構造体」1 つ。fork のために、それを最大 K 個並べた 1 つのメモリを状態にする:
     [ 共通のフレームカウンタ（8 バイト） | フレーム 0 | フレーム 1 | ... | フレーム K-1 ]
   tama_step は生きているフレームを順に tama_frame_step で 1 歩ずつ進める。
   tama_frame_step は wait を境にした状態機械:
     - 待ちフレームが残っていれば 1 減らして return 0
     - そうでなければ switch で「前回 wait した場所」の直後へ飛ぶ
     - wait に着いたら、待ちフレーム数と再開位置の番号を保存して return 0
   fork は自分のフレームを memcpy で写し、写した先を「fork の直後から再開」にするだけ *)

open Ast
open Typecheck

let buffer = Buffer.create 4096
let emit fmt = Printf.ksprintf (fun line -> Buffer.add_string buffer ("  " ^ line ^ "\n")) fmt
let label name = Buffer.add_string buffer (name ^ ":\n")

let counter = ref 0
let fresh () = incr counter; Printf.sprintf "%%t%d" !counter
let fresh_label prefix = incr counter; Printf.sprintf "%s%d" prefix !counter

(* wait と fork の再開位置。状態番号 → ラベル *)
let resume_points : (int * string) list ref = ref []

let new_resume_point prefix =
  let state = List.length !resume_points + 1 in
  let l = fresh_label prefix in
  resume_points := !resume_points @ [ (state, l) ];
  (state, l)

let slot_types : string array ref = ref [||]

let llty = function Num (I, _) | Color -> "i32" | Num (F, _) -> "double" | Bool -> "i1"

let double_constant f = Printf.sprintf "0x%016LX" (Int64.bits_of_float f)

let slot_pointer ?(frame = "%st") slot =
  let pointer = fresh () in
  emit "%s = getelementptr inbounds %%Frame, ptr %s, i32 0, i32 %d" pointer frame slot;
  pointer

let load_slot slot =
  let pointer = slot_pointer slot and value = fresh () in
  emit "%s = load %s, ptr %s" value !slot_types.(slot) pointer;
  value

let store_slot ?frame slot value =
  let pointer = slot_pointer ?frame slot in
  emit "store %s %s, ptr %s" !slot_types.(slot) value pointer

let rec expr e =
  match e.d with
  | TConstI n -> string_of_int n
  | TConstF f -> double_constant f
  | TLoad slot -> load_slot slot
  | TFrame ->
      let value = fresh () in
      emit "%s = load i32, ptr %%root" value;
      value
  | THp ->
      let value = fresh () in
      emit "%s = call double @tama_rt_hp(ptr %%bridge)" value;
      value
  | TBin (op, a, b) ->
      let a' = expr a and b' = expr b and value = fresh () in
      let instruction =
        match (op, e.ty) with
        | Add, Num (I, _) -> "add" | Sub, Num (I, _) -> "sub" | Mul, Num (I, _) -> "mul"
        | Add, _ -> "fadd" | Sub, _ -> "fsub" | Mul, _ -> "fmul" | Div, _ -> "fdiv"
      in
      emit "%s = %s %s %s, %s" value instruction (llty e.ty) a' b';
      value
  | TNeg a ->
      let a' = expr a and value = fresh () in
      (match a.ty with
      | Num (I, _) -> emit "%s = sub i32 0, %s" value a'
      | _ -> emit "%s = fneg double %s" value a');
      value
  | TToFloat a ->
      let a' = expr a and value = fresh () in
      emit "%s = sitofp i32 %s to double" value a';
      value
  | TToFrames a ->
      (* 秒 × 60 を四捨五入し、1 フレーム未満は 1 にする *)
      let a' = expr a and scaled = fresh () and rounded = fresh () and frames = fresh () and small = fresh () and value = fresh () in
      emit "%s = fmul double %s, %s" scaled a' (double_constant 60.);
      emit "%s = fadd double %s, %s" rounded scaled (double_constant 0.5);
      emit "%s = fptosi double %s to i32" frames rounded;
      emit "%s = icmp slt i32 %s, 1" small frames;
      emit "%s = select i1 %s, i32 1, i32 %s" value small frames;
      value
  | TCmp (op, a, b) ->
      let a' = expr a and b' = expr b and value = fresh () in
      (match a.ty with
      | Num (I, _) ->
          let predicate = match op with Gt -> "sgt" | Lt -> "slt" | Ge -> "sge" | Le -> "sle" in
          emit "%s = icmp %s i32 %s, %s" value predicate a' b'
      | _ ->
          let predicate = match op with Gt -> "ogt" | Lt -> "olt" | Ge -> "oge" | Le -> "ole" in
          emit "%s = fcmp %s double %s, %s" value predicate a' b');
      value
  | TCall (name, args) ->
      let args' = List.map (fun a -> llty a.ty ^ " " ^ expr a) args and value = fresh () in
      emit "%s = call %s @tama_rt_%s(%s)" value (llty e.ty) name (String.concat ", " args');
      value

(* 予約スロット *)
let state_slot = 0
let wait_slot = 1
let alive_slot = 2

let rec stmt = function
  | SStore (slot, e) -> store_slot slot (expr e)
  | SEmit (name, args) ->
      let args' = List.map (fun a -> llty a.ty ^ " " ^ expr a) args in
      emit "call void @tama_rt_%s(ptr %%bridge, %s)" name (String.concat ", " args')
  | SWait frames ->
      let frames' = expr frames in
      let remaining = fresh () in
      emit "%s = sub i32 %s, 1" remaining frames';
      store_slot wait_slot remaining;
      let state, resume = new_resume_point "resume" in
      store_slot state_slot (string_of_int state);
      emit "ret i32 0";
      label resume
  | SLoop { counter; limit; count; body } ->
      store_slot limit (expr count);
      store_slot counter "0";
      let head = fresh_label "loop" and inner = fresh_label "body" and exit = fresh_label "done" in
      emit "br label %%%s" head;
      label head;
      let i = load_slot counter and n = load_slot limit and continues = fresh () in
      emit "%s = icmp slt i32 %s, %s" continues i n;
      emit "br i1 %s, label %%%s, label %%%s" continues inner exit;
      label inner;
      List.iter stmt body;
      let i = load_slot counter and next = fresh () in
      emit "%s = add i32 %s, 1" next i;
      store_slot counter next;
      emit "br label %%%s" head;
      label exit
  | SWhile (condition, body) ->
      let head = fresh_label "while" and inner = fresh_label "body" and exit = fresh_label "done" in
      emit "br label %%%s" head;
      label head;
      let continues = expr condition in
      emit "br i1 %s, label %%%s, label %%%s" continues inner exit;
      label inner;
      List.iter stmt body;
      emit "br label %%%s" head;
      label exit
  | SFork { copies; index; body } ->
      (* 自分を「fork の直後から再開」の状態にしてから、フレーム 1〜copies-1 に丸ごと写す *)
      let state, start = new_resume_point "fork" in
      store_slot state_slot (string_of_int state);
      store_slot wait_slot "0";
      for copy = 1 to copies - 1 do
        let target = fresh () in
        emit "%s = getelementptr inbounds %%Frame, ptr %%frames, i32 %d" target copy;
        emit "call void @llvm.memcpy.p0.p0.i64(ptr %s, ptr %%st, i64 ptrtoint (ptr getelementptr (%%Frame, ptr null, i32 1) to i64), i1 false)" target;
        store_slot ~frame:target index (string_of_int copy);
        store_slot ~frame:target alive_slot "1"
      done;
      store_slot index "0";
      emit "br label %%%s" start;
      label start;
      List.iter stmt body;
      (* 本体を抜けたら、分身（index が 0 以外）は消える *)
      let i = load_slot index and original = fresh () in
      let continue_label = fresh_label "fork_done" and vanish = fresh_label "vanish" in
      emit "%s = icmp eq i32 %s, 0" original i;
      emit "br i1 %s, label %%%s, label %%%s" original continue_label vanish;
      label vanish;
      store_slot alive_slot "0";
      emit "ret i32 0";
      label continue_label

let runtime_declarations =
  {|declare ptr @calloc(i64, i64)
declare void @free(ptr)
declare void @llvm.memcpy.p0.p0.i64(ptr, ptr, i64, i1)
declare double @tama_rt_hp(ptr)
declare void @tama_rt_ring(ptr, i32, double, double, i32)
declare void @tama_rt_aim(ptr, double, i32)
declare i32 @tama_rt_hue(i32, double)
declare i32 @tama_rt_mix(i32, i32, double)
declare double @tama_rt_ease_out(double)
|}

let program ~name ~export (body, slots, copies) =
  Buffer.clear buffer;
  counter := 0;
  resume_points := [];
  slot_types :=
    Array.of_list ([ "i32"; "i32"; "i32" ] @ List.map (function SlotInt -> "i32" | SlotFloat -> "double") slots);
  (* 本体を先に生成し、再開位置の一覧が揃ってから入口の switch を書く *)
  label "start";
  List.iter stmt body;
  store_slot state_slot "-1";
  emit "ret i32 1";
  let body_ir = Buffer.contents buffer in
  Buffer.clear buffer;
  let field_types = String.concat ", " (Array.to_list !slot_types) in
  (* 1 スロット 8 バイトとして多めに見積もる（実際の %Frame はこれ以下） *)
  let frame_size = 8 * Array.length !slot_types in
  let state_size = 8 + (copies * frame_size) in
  let switch_cases =
    String.concat " " (List.map (fun (state, l) -> Printf.sprintf "i32 %d, label %%%s" state l) !resume_points)
  in
  (* 生きているフレームを順に進める。フレーム 0 が終わったらボスの終わり *)
  let step_frames =
    String.concat ""
      (List.init copies (fun i ->
           Printf.sprintf
             {|frame%d:
  %%f%d = getelementptr inbounds %%Frame, ptr %%frames, i32 %d
  %%alive%d.ptr = getelementptr inbounds %%Frame, ptr %%f%d, i32 0, i32 2
  %%alive%d = load i32, ptr %%alive%d.ptr
  %%live%d = icmp ne i32 %%alive%d, 0
  br i1 %%live%d, label %%run%d, label %%next%d
run%d:
  %%r%d = call i32 @tama_frame_step(ptr %%f%d, ptr %%root, ptr %%bridge)
  br label %%next%d
next%d:
  %%done%d = phi i32 [ %s, %%frame%d ], [ %s, %%run%d ]
|}
             i i i i i i i i i i i i i i i i i i
             (if i = 0 then "0" else Printf.sprintf "%%done%d" (i - 1)) i
             (if i = 0 then Printf.sprintf "%%r%d" i else Printf.sprintf "%%done%d" (i - 1)) i
           ^ if i + 1 < copies then Printf.sprintf "  br label %%frame%d\n" (i + 1) else ""))
  in
  Printf.sprintf
    {|; Tama compiler output for boss %s
%%Frame = type { %s }

%s
define %sptr @tama_new() {
  %%root = call ptr @calloc(i64 1, i64 %d)
  %%frames = getelementptr inbounds i8, ptr %%root, i64 8
  %%alive.ptr = getelementptr inbounds %%Frame, ptr %%frames, i32 0, i32 2
  store i32 1, ptr %%alive.ptr
  ret ptr %%root
}

define %svoid @tama_free(ptr %%root) {
  call void @free(ptr %%root)
  ret void
}

; ボスの状態はこの大きさのメモリがすべて（分身の分も含む）。memcpy するだけで保存も巻き戻しもできる
define %si64 @tama_state_size() {
  ret i64 %d
}

; 0: 継続中、1: 終了
define %si32 @tama_step(ptr %%root, ptr %%bridge) {
entry:
  %%frame.old = load i32, ptr %%root
  %%frame.new = add i32 %%frame.old, 1
  store i32 %%frame.new, ptr %%root
  %%frames = getelementptr inbounds i8, ptr %%root, i64 8
  br label %%frame0
%s  ret i32 %%done%d
}

define internal i32 @tama_frame_step(ptr %%st, ptr %%root, ptr %%bridge) {
entry:
  %%frames = getelementptr inbounds i8, ptr %%root, i64 8
  %%wait.ptr = getelementptr inbounds %%Frame, ptr %%st, i32 0, i32 1
  %%wait = load i32, ptr %%wait.ptr
  %%waiting = icmp sgt i32 %%wait, 0
  br i1 %%waiting, label %%still_waiting, label %%dispatch
still_waiting:
  %%wait.next = sub i32 %%wait, 1
  store i32 %%wait.next, ptr %%wait.ptr
  ret i32 0
dispatch:
  %%state.ptr = getelementptr inbounds %%Frame, ptr %%st, i32 0, i32 0
  %%state = load i32, ptr %%state.ptr
  switch i32 %%state, label %%finished [ i32 0, label %%start %s ]
finished:
  ret i32 1
%s}
|}
    name field_types runtime_declarations export state_size export export state_size export step_frames
    (copies - 1) switch_cases body_ir
