{
open Parser

exception Error of string * Lexing.position

let keywords =
  [ ("boss", BOSS); ("let", LET); ("wait", WAIT); ("repeat", REPEAT); ("forever", FOREVER);
    ("every", EVERY); ("as", AS); ("until", UNTIL); ("during", DURING); ("fork", FORK);
    ("at", AT); ("hp", HP); ("time", TIME) ]

(* 単位を基本単位（m、秒、ラジアン）に直す倍率と次元。時間は 60 フレーム = 1 秒 *)
let unit_of name =
  let open Ast in
  match name with
  | "m" -> Some (1., { m = 1; s = 0; deg = 0 })
  | "s" -> Some (1., { m = 0; s = 1; deg = 0 })
  | "ms" -> Some (0.001, { m = 0; s = 1; deg = 0 })
  | "frames" | "frame" -> Some (1. /. 60., { m = 0; s = 1; deg = 0 })
  | "deg" -> Some (Float.pi /. 180., { m = 0; s = 0; deg = 1 })
  | _ -> None

let unit_error lexbuf name = raise (Error (Printf.sprintf "知らない単位 %s" name, Lexing.lexeme_start_p lexbuf))
}

let digit = ['0'-'9']
let int = digit+
let float = digit+ '.' digit+
let hex = ['0'-'9' 'a'-'f' 'A'-'F']
let ident = ['a'-'z' 'A'-'Z' '_'] ['a'-'z' 'A'-'Z' '0'-'9' '_']*
let unit_name = ['a'-'z']+

rule token = parse
  | [' ' '\t' '\r'] { token lexbuf }
  | '\n' { Lexing.new_line lexbuf; token lexbuf }
  | "//" [^ '\n']* { token lexbuf }
  (* 単位付きの数。2m/s、30deg、0.2s、500ms、2frames。最長一致なので 2m/s は 1 つのトークンになる *)
  | (int | float) as n (unit_name as u) ('/' (unit_name as per))? {
      let value = float_of_string n in
      match (unit_of u, per) with
      | None, _ -> unit_error lexbuf u
      | Some (scale, dim), None -> NUM (value *. scale, dim)
      | Some (scale, dim), Some per -> (
          match unit_of per with
          | None -> unit_error lexbuf per
          | Some (scale', dim') ->
              NUM (value *. scale /. scale', Ast.{ m = dim.m - dim'.m; s = dim.s - dim'.s; deg = dim.deg - dim'.deg })) }
  | (int | float) as n '%' { NUM (float_of_string n /. 100., Ast.{ m = 0; s = 0; deg = 0 }) }
  | float as n { NUM (float_of_string n, Ast.{ m = 0; s = 0; deg = 0 }) }
  | int as n { INT (int_of_string n) }
  | '#' (hex hex hex hex hex hex as h) { COLOR (int_of_string ("0x" ^ h)) }
  | ident as s { match List.assoc_opt s keywords with Some k -> k | None -> IDENT s }
  | "→" | "->" { ARROW }
  | '{' { LBRACE } | '}' { RBRACE } | '(' { LPAREN } | ')' { RPAREN }
  | ':' { COLON } | ',' { COMMA } | '=' { EQ }
  | '+' { PLUS } | '-' { MINUS } | '*' { STAR } | '/' { SLASH }
  | ">=" { GE } | "<=" { LE } | '>' { GT } | '<' { LT }
  | eof { EOF }
  | _ as c { raise (Error (Printf.sprintf "予期しない文字 '%c'" c, Lexing.lexeme_start_p lexbuf)) }
