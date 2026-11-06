namespace BossFSharp

open System

// 単位。度とラジアン、速さ（ワールド単位 / 秒）の取り違えはコンパイルエラーになる
[<Measure>] type deg
[<Measure>] type rad
[<Measure>] type m
[<Measure>] type s
[<Measure>] type frame

/// 弾幕は「値」。手続きではなくデータとして組み立てて、あとで解釈する
type Pattern =
    | Ring of count: int * speed: float<m/s>
    | Rotate of float<deg> * Pattern
    /// times 回繰り返す。1 周ごとに step ずつ回す
    | Repeat of times: int * step: float<deg> * Pattern
    | Then of Pattern * Pattern
    | Wait of int<frame>

/// Unity 側に渡す命令
type Command =
    | Spawn of angle: float<rad> * speed: float<m/s> * rgba: uint32
    | Sleep of int<frame>

module Boss =
    let toRad (angle: float<deg>) : float<rad> = angle * (Math.PI / 180.0) * 1.0<rad/deg>

    let (|Calm|Angry|Desperate|) hpRatio =
        if hpRatio > 0.6 then Calm elif hpRatio > 0.3 then Angry else Desperate

    let colorOf = function
        | Calm -> 0x66CCFFFFu
        | Angry -> 0xFFAA33FFu
        | Desperate -> 0xFF3355FFu

    // 弾幕は値なので、名前を付けて組み合わせられる
    let spiral = Repeat (24, 15.0<deg>, Then (Ring (12, 2.0<m/s>), Wait 6<frame>))
    let flower = Repeat (6, 6.0<deg>, Then (Ring (30, 3.0<m/s>), Then (Rotate (6.0<deg>, Ring (30, 2.2<m/s>)), Wait 20<frame>)))

    /// Pattern を解釈して命令列にする
    let rec run (hpRatio: unit -> float) (rotation: float<deg>) (pattern: Pattern) : seq<Command> =
        seq {
            match pattern with
            | Ring (count, speed) ->
                for i in 0 .. count - 1 do
                    let angle = rotation + 360.0<deg> * float i / float count
                    yield Spawn (toRad angle, speed, colorOf (hpRatio ()))
            | Rotate (delta, inner) -> yield! run hpRatio (rotation + delta) inner
            | Repeat (times, step, inner) ->
                for i in 0 .. times - 1 do
                    yield! run hpRatio (rotation + step * float i) inner
            | Then (first, second) ->
                yield! run hpRatio rotation first
                yield! run hpRatio rotation second
            | Wait frames -> yield Sleep frames
        }

    /// 終わらないボス。周回のたびに HP を見て弾幕を選ぶ。C# からは IEnumerable<Command> に見える
    let create (hpRatio: Func<float>) : seq<Command> =
        let hp () = hpRatio.Invoke ()
        Seq.initInfinite id
        |> Seq.collect (fun _ ->
            match hp () with
            | Calm -> run hp 0.0<deg> spiral
            | Angry -> run hp 0.0<deg> flower
            | Desperate -> run hp 0.0<deg> (Then (spiral, flower)))
