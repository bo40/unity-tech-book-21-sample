(* Tama の構文木。パーサが作り、Typecheck が型と単位を検査する *)

type pos = Lexing.position
type binop = Add | Sub | Mul | Div
type cmpop = Gt | Lt | Ge | Le

(* 単位の次元。長さ（m）、時間（s）、角度（deg）の指数。m/s なら { m = 1; s = -1; deg = 0 } *)
type dim = { m : int; s : int; deg : int }

type expr = { desc : expr_desc; pos : pos }

and expr_desc =
  | Int of int
  | Num of float * dim  (* 単位付きの数。値は基本単位（m、秒、ラジアン）に直してある *)
  | Color of int  (* #ff3366 → 0xff3366 *)
  | Var of string
  | Time  (* 起動からの時間（秒） *)
  | Hp  (* ボスの残り HP の割合 *)
  | Binop of binop * expr * expr
  | Neg of expr
  | Call of string * expr list  (* ease_out(t) *)
  | Gradient of expr * expr * expr  (* (a → b) at t *)

type cond = { left : expr; op : cmpop; right : expr }

(* 弾を出す命令の引数。ring 36, speed: 2m/s の 36 が Positional、speed: 2m/s が Named *)
type arg = Positional of expr | Named of string * expr

type stmt = { sdesc : stmt_desc; spos : pos }

and stmt_desc =
  | Let of string * expr
  | Wait of expr
  | Emit of string * arg list
  | Repeat of expr option * expr option * string option * stmt list  (* 回数（None は forever）、every、as、本体 *)
  | Until of cond * stmt list
  | During of expr * expr option * string * stmt list  (* 長さ、every、as、本体 *)
  | Fork of int * string * stmt list  (* fork N as i { } *)

type program = { name : string; body : stmt list }
