//! .wasm を端末向けに事前コンパイルする: cargo run --example precompile -- boss.wasm boss.cwasm aarch64-linux-android

fn main() -> wasmtime::Result<()> {
    let args: Vec<String> = std::env::args().collect();
    let [_, input, output, target] = args.as_slice() else {
        panic!("usage: precompile <input.wasm> <output.cwasm> <target-triple>");
    };
    let started = std::time::Instant::now();
    let compiled = unihub::precompile(&std::fs::read(input)?, target)?;
    std::fs::write(output, &compiled)?;
    println!("{target}: {} bytes in {:?}", compiled.len(), started.elapsed());
    Ok(())
}
