// ボス 7: Go ─ Goroutine Swarm。
// 1 機 1 goroutine の砲台（ドローン）がボスのまわりを回り、それぞれ自分のリズムで撃つ。
// 隊形の切り替えは、指揮役の goroutine がチャネルを close して全機に一斉に知らせる。
// GOOS=wasip1 GOARCH=wasm でビルドし、OCaml と同じ wasmtime で動かす
package main

import "math"

const drones = 1000

// Formation は隊形。changed が close されたら next に乗り換える
type Formation struct {
	radius  func(theta float64) float64
	spin    float64 // 1 フレームあたりの回転（ラジアン）
	speed   float64
	twist   float64 // 撃つ向きを外向きからずらす角度
	next    *Formation
	changed chan struct{}
}

var formations = []*Formation{
	{radius: func(float64) float64 { return 1.4 }, spin: 0.004, speed: 1.6},
	{radius: func(t float64) float64 { return 1.1 + 0.5*math.Sin(5*t) }, spin: -0.006, speed: 2.0, twist: 0.6},
	{radius: func(t float64) float64 { return 0.4 + 1.4*math.Mod(t*3/(2*math.Pi), 1) }, spin: 0.01, speed: 2.4, twist: -0.9},
}

func drone(id int, f *Formation, clock *Clock) {
	base := 2 * math.Pi * float64(id) / drones
	hue := math.Mod(float64(id)*0.618033988749895, 1) // 黄金比ずつ色相をずらす
	period := 120 + id%8*20                           // 撃つ間隔は 120〜260 フレーム。機体ごとに違う
	<-clock.After(1 + id%period)
	for {
		select {
		case <-f.changed: // 指揮役が隊形を切り替えた
			f = f.next
		default:
		}
		theta := base + f.spin*float64(clock.Now())
		r := f.radius(base)
		spawnAt(r*math.Cos(theta), r*math.Sin(theta)*0.6, theta+f.twist, f.speed, rgbOfHue(hue))
		<-clock.After(period)
	}
}

// conductor は HP が減るたびに次の隊形を知らせる。close は待っている全員に届く
func conductor(f *Formation, clock *Clock) {
	for i, threshold := range []float64{0.66, 0.33} {
		for hp() > threshold {
			<-clock.After(10)
		}
		f.next = formations[i+1]
		close(f.changed)
		f = f.next
	}
	<-clock.After(math.MaxInt32)
}

func main() {
	for _, f := range formations {
		f.changed = make(chan struct{})
	}
	clock := NewClock()
	for id := range drones {
		clock.Go(func() { drone(id, formations[0], clock) })
	}
	clock.Go(func() { conductor(formations[0], clock) })
	for {
		clock.Tick()
		frameEnd()
	}
}

// rgbOfHue は色相 h（0〜1）を、少し淡い RGB にする
func rgbOfHue(h float64) int32 {
	channel := func(offset float64) int32 {
		k := math.Mod(h*6+offset, 6)
		v := 1 - math.Max(0, math.Min(1, math.Min(k, 4-k)))
		return int32((0.35 + 0.65*v) * 255)
	}
	return channel(5)<<16 | channel(3)<<8 | channel(1)
}
