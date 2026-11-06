%{
open Ast
let mk desc pos = { desc; pos }
%}

%token <int> INT COLOR
%token <float * Ast.dim> NUM
%token <string> IDENT
%token BOSS LET WAIT REPEAT FOREVER EVERY AS UNTIL DURING FORK AT HP TIME
%token LBRACE RBRACE LPAREN RPAREN COLON COMMA EQ ARROW
%token PLUS MINUS STAR SLASH GT LT GE LE EOF

%nonassoc AT  /* (a → b) at t + 1 は (a → b) at (t + 1) */
%left PLUS MINUS
%left STAR SLASH
%nonassoc UMINUS

%start <Ast.program> program

%%

program:
  | BOSS name = IDENT body = block EOF { { name; body } }

block:
  | LBRACE body = list(stmt) RBRACE { body }

stmt:
  | d = stmt_desc { { sdesc = d; spos = $startpos } }

stmt_desc:
  | LET x = IDENT EQ e = expr { Let (x, e) }
  | WAIT e = expr { Wait e }
  | f = IDENT args = separated_nonempty_list(COMMA, arg) { Emit (f, args) }
  | REPEAT n = repeat_count every = every x = alias b = block { Repeat (n, every, x, b) }
  | UNTIL c = cond b = block { Until (c, b) }
  | DURING d = expr every = every AS x = IDENT b = block { During (d, every, x, b) }
  | FORK n = INT AS x = IDENT b = block { Fork (n, x, b) }

repeat_count:
  | FOREVER { None }
  | n = expr { Some n }

every:
  | { None }
  | EVERY e = expr { Some e }

alias:
  | { None }
  | AS x = IDENT { Some x }

arg:
  | k = IDENT COLON e = expr { Named (k, e) }
  | e = expr { Positional e }

cond:
  | left = expr op = cmpop right = expr { { left; op; right } }

cmpop:
  | GT { Gt } | LT { Lt } | GE { Ge } | LE { Le }

expr:
  | n = INT { mk (Int n) $startpos }
  | n = NUM { mk (Num (fst n, snd n)) $startpos }
  | c = COLOR { mk (Color c) $startpos }
  | x = IDENT { mk (Var x) $startpos }
  | TIME { mk Time $startpos }
  | HP { mk Hp $startpos }
  | l = expr PLUS r = expr { mk (Binop (Add, l, r)) $startpos }
  | l = expr MINUS r = expr { mk (Binop (Sub, l, r)) $startpos }
  | l = expr STAR r = expr { mk (Binop (Mul, l, r)) $startpos }
  | l = expr SLASH r = expr { mk (Binop (Div, l, r)) $startpos }
  | MINUS e = expr %prec UMINUS { mk (Neg e) $startpos }
  | LPAREN e = expr RPAREN { e }
  | f = IDENT LPAREN args = separated_list(COMMA, expr) RPAREN { mk (Call (f, args)) $startpos }
  (* (#00ffff → #ff00ff) at t: グラデーションの途中の色 *)
  | LPAREN a = expr ARROW b = expr RPAREN AT t = expr { mk (Gradient (a, b, t)) $startpos }
