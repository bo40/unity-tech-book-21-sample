(* 型と単位の検査、および脱糖。結果は Codegen がそのまま LLVM IR にできる形（Tir）にする。
   変数はすべてフレーム構造体のスロットに置く。wait をまたいでも値が残るので、
   状態機械への変換が「スロットに書いて return、switch で戻ってくる」だけで済む。 *)

open Ast

exception Error of string * pos

type num = I | F
type ty = Num of num * dim | Color | Bool

let dimless = { m = 0; s = 0; deg = 0 }
let seconds = { m = 0; s = 1; deg = 0 }
let angle = { m = 0; s = 0; deg = 1 }
let speed = { m = 1; s = -1; deg = 0 }

(* 次元を m/s、deg/s、s のように表示する *)
let show_dim d =
  let part name n = if n = 0 then [] else if abs n = 1 then [ name ] else [ Printf.sprintf "%s^%d" name (abs n) ] in
  let pick sign = List.concat [ part "m" (if sign * d.m > 0 then d.m else 0);
                                part "deg" (if sign * d.deg > 0 then d.deg else 0);
                                part "s" (if sign * d.s > 0 then d.s else 0) ] in
  match (pick 1, pick (-1)) with
  | [], [] -> "単位なし"
  | up, [] -> String.concat "·" up
  | [], down -> "1/" ^ String.concat "·" down
  | up, down -> String.concat "·" up ^ "/" ^ String.concat "·" down

let show_ty = function
  | Num (_, d) -> show_dim d
  | Color -> "色"
  | Bool -> "真偽値"

let error pos fmt = Printf.ksprintf (fun message -> raise (Error (message, pos))) fmt

(* ---- 型付き中間表現 ---- *)

type slot_kind = SlotInt | SlotFloat

type texpr = { d : tdesc; ty : ty }

and tdesc =
  | TConstI of int
  | TConstF of float
  | TLoad of int
  | TFrame  (* 起動からのフレーム数（全分身で共通） *)
  | THp
  | TBin of binop * texpr * texpr
  | TNeg of texpr
  | TToFloat of texpr
  | TToFrames of texpr  (* 秒をフレーム数（1 以上の整数）にする *)
  | TCmp of cmpop * texpr * texpr
  | TCall of string * texpr list

type tstmt =
  | SStore of int * texpr
  | SLoop of { counter : int; limit : int; count : texpr; body : tstmt list }
  | SWhile of texpr * tstmt list
  | SWait of texpr  (* フレーム数（整数） *)
  | SEmit of string * texpr list
  | SFork of { copies : int; index : int; body : tstmt list }

(* スロット 0〜2 は状態番号、残り待ちフレーム、生きているか（分身用）で予約済み *)
let reserved_slots = 3
let slots : slot_kind list ref = ref []

let new_slot kind =
  slots := !slots @ [ kind ];
  reserved_slots + List.length !slots - 1

let slot_kind_of_ty pos = function
  | Num (I, _) | Color -> SlotInt
  | Num (F, _) -> SlotFloat
  | Bool -> error pos "真偽値は変数に入れられません"

(* ---- 式 ---- *)

let num_of pos e =
  match e.ty with Num (n, d) -> (n, d) | ty -> error pos "数が必要ですが %s です" (show_ty ty)

let to_float e = match e.ty with Num (I, d) -> { d = TToFloat e; ty = Num (F, d) } | _ -> e
let add_dim a b = { m = a.m + b.m; s = a.s + b.s; deg = a.deg + b.deg }
let sub_dim a b = { m = a.m - b.m; s = a.s - b.s; deg = a.deg - b.deg }

let rec expr env e =
  match e.desc with
  | Int n -> { d = TConstI n; ty = Num (I, dimless) }
  | Num (x, d) -> { d = TConstF x; ty = Num (F, d) }
  | Color c -> { d = TConstI c; ty = Color }
  | Var x -> (
      match List.assoc_opt x env with
      | Some (slot, ty) -> { d = TLoad slot; ty }
      | None -> error e.pos "未定義の変数 %s" x)
  | Time ->
      { d = TBin (Div, { d = TToFloat { d = TFrame; ty = Num (I, dimless) }; ty = Num (F, dimless) },
                  { d = TConstF 60.; ty = Num (F, dimless) });
        ty = Num (F, seconds) }
  | Hp -> { d = THp; ty = Num (F, dimless) }
  | Neg a ->
      let a = expr env a in
      ignore (num_of e.pos a);
      { d = TNeg a; ty = a.ty }
  | Binop (op, a, b) -> binop e.pos op (expr env a) (expr env b)
  | Call (name, args) -> call e.pos name (List.map (expr env) args)
  | Gradient (a, b, t) ->
      let a = expr env a and b = expr env b and t = expr env t in
      if a.ty <> Color || b.ty <> Color then error e.pos "→ の両側は色です（%s → %s）" (show_ty a.ty) (show_ty b.ty);
      (match t.ty with Num (_, d) when d = dimless -> () | ty -> error e.pos "at の後ろは 0〜1 の数です（%s）" (show_ty ty));
      { d = TCall ("mix", [ a; b; to_float t ]); ty = Color }

(* 単位の規則: 足し引きは同じ次元どうし、掛け算と割り算は次元の指数を足し引きする。
   色に角度を足し引きすると色相が回る *)
and binop pos op a b =
  match (op, a.ty, b.ty) with
  | (Add | Sub), Color, Num (_, d) when d = angle ->
      let turn = to_float b in
      let turn = if op = Sub then { d = TNeg turn; ty = turn.ty } else turn in
      { d = TCall ("hue", [ a; turn ]); ty = Color }
  | _ ->
      let na, da = num_of pos a and nb, db = num_of pos b in
      let symbol = match op with Add -> "+" | Sub -> "-" | Mul -> "*" | Div -> "/" in
      let mismatch () = error pos "単位が合いません: %s %s %s" (show_ty a.ty) symbol (show_ty b.ty) in
      let dim =
        match op with
        | Add | Sub -> if da = db then da else mismatch ()
        | Mul -> add_dim da db
        | Div -> sub_dim da db
      in
      (* 割り算は常に浮動小数点数（7 / 2 を 3 にしないため） *)
      let n = match op with Div -> F | _ -> if na = I && nb = I then I else F in
      let a, b = if n = F then (to_float a, to_float b) else (a, b) in
      { d = TBin (op, a, b); ty = Num (n, dim) }

and call pos name args =
  match (name, args) with
  | "ease_out", [ t ] when t.ty = Num (I, dimless) || t.ty = Num (F, dimless) ->
      { d = TCall (name, [ to_float t ]); ty = Num (F, dimless) }
  | "ease_out", _ -> error pos "ease_out は単位なしの数を 1 つ取ります"
  | _ -> error pos "未定義の関数 %s" name

let cond env c =
  let l = expr env c.left and r = expr env c.right in
  let nl, dl = num_of c.left.pos l and nr, dr = num_of c.right.pos r in
  if dl <> dr then error c.left.pos "比較の単位が合いません: %s と %s" (show_ty l.ty) (show_ty r.ty);
  let l, r = if nl = I && nr = I then (l, r) else (to_float l, to_float r) in
  { d = TCmp (c.op, l, r); ty = Bool }

let negate = function Gt -> Le | Lt -> Ge | Ge -> Lt | Le -> Gt

(* 時間（秒）の式をフレーム数にする *)
let frames_of pos what e =
  match e.ty with
  | Num (_, d) when d = seconds -> { d = TToFrames (to_float e); ty = Num (I, dimless) }
  | Num (_, d) when d = dimless -> error pos "%s には時間の単位を付けてください（例: 0.2s、10frames）" what
  | ty -> error pos "%s には時間が必要ですが %s です" what (show_ty ty)

(* ---- 文 ---- *)

(* 弾を出す命令の引数: 名前（位置引数は ""）、次元（None は整数）、既定値 *)
let emit_params = function
  | "ring" ->
      Some
        [ ("", None, None);
          ("speed", Some speed, None);
          ("turn", Some angle, Some { d = TConstF 0.; ty = Num (F, angle) });
          ("color", Some dimless, Some { d = TConstI 0xffffff; ty = Color }) ]
  | "aim" ->
      Some [ ("speed", Some speed, None); ("color", Some dimless, Some { d = TConstI 0xffffff; ty = Color }) ]
  | _ -> None

(* 本体のどこかで時間が進むか。進まないループは 1 フレームの中で永久に回ってしまう *)
let rec waits stmts =
  List.exists
    (fun s ->
      match s.sdesc with
      | Wait _ | Repeat (_, Some _, _, _) | During (_, Some _, _, _) -> true
      | Repeat (_, None, _, b) | Until (_, b) | During (_, None, _, b) | Fork (_, _, b) -> waits b
      | Let _ | Emit _ -> false)
    stmts

let max_fork = ref 1
let in_fork = ref false

let rec block env stmts =
  let _, out =
    List.fold_left
      (fun (env, acc) s ->
        let env, compiled = stmt env s in
        (env, acc @ compiled))
      (env, []) stmts
  in
  out

and every_wait env what = function
  | None -> []
  | Some e -> [ SWait (frames_of e.pos what (expr env e)) ]

and stmt env s =
  match s.sdesc with
  | Let (x, e) ->
      let e = expr env e in
      let slot = new_slot (slot_kind_of_ty s.spos e.ty) in
      ((x, (slot, e.ty)) :: env, [ SStore (slot, e) ])
  | Wait e -> (env, [ SWait (frames_of s.spos "wait" (expr env e)) ])
  | Emit (name, args) -> (env, [ emit s.spos env name args ])
  | Repeat (count, every, name, body) ->
      let counter = new_slot SlotInt in
      let inner = match name with Some x -> (x, (counter, Num (I, dimless))) :: env | None -> env in
      let body' = block inner body @ every_wait env "every" every in
      (match count with
      | None ->
          if every = None && not (waits body) then
            error s.spos "repeat forever に every も wait もないと、1 フレームの中で永久に回ってしまいます";
          (* forever は数え続けるだけの while にする *)
          let next = { d = TBin (Add, { d = TLoad counter; ty = Num (I, dimless) }, { d = TConstI 1; ty = Num (I, dimless) }); ty = Num (I, dimless) } in
          (env, [ SStore (counter, { d = TConstI 0; ty = Num (I, dimless) });
                  SWhile ({ d = TConstI 1; ty = Bool }, body' @ [ SStore (counter, next) ]) ])
      | Some count ->
          let count = expr env count in
          if count.ty <> Num (I, dimless) then
            error s.spos "repeat の回数は単位なしの整数です（%s）" (show_ty count.ty);
          let limit = new_slot SlotInt in
          (env, [ SLoop { counter; limit; count; body = body' } ]))
  | Until (c, body) ->
      if not (waits body) then error s.spos "until の中で時間が進まないと、1 フレームの中で永久に回ってしまいます";
      let c = cond env c in
      let op = match c.d with TCmp (op, _, _) -> op | _ -> assert false in
      let negated = match c.d with TCmp (_, l, r) -> { c with d = TCmp (negate op, l, r) } | _ -> c in
      (env, [ SWhile (negated, block env body) ])
  | During (duration, every, name, body) ->
      (* during D every I as t { body } を次のように脱糖する:
           let start = frame; let length = D をフレームにしたもの
           while frame - start < length { let t = (frame - start) / length; body; wait I } *)
      if every = None && not (waits body) then
        error s.spos "during に every も wait もないと、1 フレームの中で永久に回ってしまいます";
      let length_frames = frames_of s.spos "during" (expr env duration) in
      let start = new_slot SlotInt and length = new_slot SlotInt and t = new_slot SlotFloat in
      let int_dimless d = { d; ty = Num (I, dimless) } in
      let elapsed = int_dimless (TBin (Sub, int_dimless TFrame, int_dimless (TLoad start))) in
      let progress = { d = TBin (Div, to_float elapsed, to_float (int_dimless (TLoad length))); ty = Num (F, dimless) } in
      let inner = (name, (t, Num (F, dimless))) :: env in
      ( env,
        [ SStore (start, int_dimless TFrame);
          SStore (length, length_frames);
          SWhile ({ d = TCmp (Lt, elapsed, int_dimless (TLoad length)); ty = Bool },
                  (SStore (t, progress) :: block inner body) @ every_wait env "every" every) ] )
  | Fork (copies, name, body) ->
      if copies < 1 then error s.spos "fork の数は 1 以上です";
      if !in_fork then error s.spos "fork の中で fork は使えません";
      in_fork := true;
      let index = new_slot SlotInt in
      let body = block ((name, (index, Num (I, dimless))) :: env) body in
      in_fork := false;
      max_fork := max !max_fork copies;
      (env, [ SFork { copies; index; body } ])

and emit pos env name args =
  match emit_params name with
  | None -> error pos "未定義の命令 %s" name
  | Some params ->
      let positional = List.filter_map (function Positional e -> Some e | Named _ -> None) args in
      let named = List.filter_map (function Named (k, e) -> Some (k, e) | Positional _ -> None) args in
      List.iter
        (fun (k, _) ->
          if not (List.exists (fun (p, _, _) -> p = k) params) then error pos "%s に %s という引数はありません" name k)
        named;
      let expected_positional = List.length (List.filter (fun (p, _, _) -> p = "") params) in
      if List.length positional <> expected_positional then
        error pos "%s の最初の引数（名前のないもの）は %d 個です" name expected_positional;
      let compiled =
        List.map
          (fun (p, expected, default) ->
            let given = if p = "" then Some (List.hd positional) else List.assoc_opt p named in
            match (given, default) with
            | None, Some d -> d
            | None, None -> error pos "%s に %s がありません" name p
            | Some e, _ -> (
                let e = expr env e in
                let label = if p = "" then "数" else p in
                match (expected, e.ty) with
                | None, Num (I, d) when d = dimless -> e
                | Some _, Color when p = "color" -> e
                | Some d, Num (_, d') when d = d' && p <> "color" -> to_float e
                | Some d, ty when p <> "color" -> error pos "%s の %s は %s です（%s が渡されました）" name label (show_dim d) (show_ty ty)
                | _, ty -> error pos "%s の %s の型が違います（%s）" name label (show_ty ty)))
          params
      in
      SEmit (name, compiled)

let program (p : program) =
  slots := [];
  max_fork := 1;
  in_fork := false;
  let body = block [] p.body in
  (body, !slots, !max_fork)
