package main

import "sync"

// Clock はフレームで数える時計。time.After のかわりに、n フレーム後に閉じるチャネルを返す。
// 毎フレーム全員を起こすのではなく、そのフレームに起きる番の goroutine だけを起こす
type Clock struct {
	mu      sync.Mutex
	now     int
	waiters map[int][]chan struct{} // 起きるフレーム → 待っているチャネル
	busy    sync.WaitGroup          // 起こしたが、まだ次の待ちに入っていない goroutine の数
}

func NewClock() *Clock {
	return &Clock{waiters: map[int][]chan struct{}{}}
}

// Go は goroutine を起動する。起動した goroutine は、最初に After を呼ぶまでこのフレームの仕事をしているとみなす
func (c *Clock) Go(f func()) {
	c.busy.Add(1)
	go f()
}

// After は n フレーム後（n >= 1）に閉じるチャネルを返す。
// 呼んだ goroutine は、このフレームの仕事を終えて待ちに入ったとみなす
func (c *Clock) After(n int) <-chan struct{} {
	ch := make(chan struct{})
	c.mu.Lock()
	c.waiters[c.now+n] = append(c.waiters[c.now+n], ch)
	c.mu.Unlock()
	c.busy.Done()
	return ch
}

// Now は今のフレーム番号
func (c *Clock) Now() int {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.now
}

// Tick は時計を 1 フレーム進め、起きる番の goroutine を起こし、全員がまた待ちに入るまで待つ
func (c *Clock) Tick() int {
	c.mu.Lock()
	c.now++
	due := c.waiters[c.now]
	delete(c.waiters, c.now)
	c.busy.Add(len(due))
	c.mu.Unlock()
	for _, ch := range due {
		close(ch)
	}
	c.busy.Wait()
	return len(due)
}
