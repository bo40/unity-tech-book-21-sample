(* Tama コンパイラ: tamac [--windows] input.tama -o output.ll *)

let print_position (pos : Lexing.position) =
  Printf.sprintf "%s:%d:%d" pos.pos_fname pos.pos_lnum (pos.pos_cnum - pos.pos_bol + 1)

let () =
  let input = ref "" and output = ref "" and windows = ref false in
  Arg.parse
    [ ("-o", Arg.Set_string output, "output .ll file");
      ("--windows", Arg.Set windows, "export symbols with dllexport (Windows DLL)") ]
    (fun file -> input := file)
    "tamac [--windows] input.tama -o output.ll";
  let channel = open_in !input in
  let lexbuf = Lexing.from_channel channel in
  Lexing.set_filename lexbuf !input;
  let fail pos message =
    Printf.eprintf "%s: error: %s\n" (print_position pos) message;
    exit 1
  in
  let ast =
    try Parser.program Lexer.token lexbuf with
    | Lexer.Error (message, pos) -> fail pos message
    | Parser.Error -> fail lexbuf.lex_start_p "構文エラー"
  in
  close_in channel;
  let typed = try Typecheck.program ast with Typecheck.Error (message, pos) -> fail pos message in
  let export = if !windows then "dllexport " else "" in
  let ir = Codegen.program ~name:ast.name ~export typed in
  if !output = "" then print_string ir
  else begin
    let out = open_out !output in
    output_string out ir;
    close_out out
  end
