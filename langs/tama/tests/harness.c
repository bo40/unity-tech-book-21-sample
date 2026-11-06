// Unity なしで Tama のボスを動かすテスト。HP は 600 フレームで 1.0 → 0.0 に減らす。
// 途中で状態を memcpy で保存し、あとで戻して同じ弾が出るか（巻き戻せるか）も確かめる
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../../common/unibridge.h"

void *tama_new(void);
int tama_step(void *state, UniBridge *bridge);
void tama_free(void *state);
long long tama_state_size(void);

static int frame, spawned;
static unsigned long long checksum;

static void spawn(float x, float y, float angle, float speed, float accel, uint32_t rgba, void *user) {
    spawned++;
    checksum = checksum * 31 + (unsigned long long)(angle * 1000) + rgba;
}
static float hp_ratio(void *user) { float hp = 1.0f - frame / 600.0f; return hp > 0 ? hp : 0; }
static float aim_angle(void *user) { return -1.5707963f; }

static void run(void *boss, UniBridge *bridge, int frames) {
    for (int i = 0; i < frames; i++, frame++) tama_step(boss, bridge);
}

int main(void) {
    UniBridge bridge = { spawn, hp_ratio, aim_angle, NULL };
    void *boss = tama_new();
    long long size = tama_state_size();

    run(boss, &bridge, 500);  // fork（HP 25% 未満 = 450 フレーム目以降）のあとまで進める
    void *saved = malloc(size);
    memcpy(saved, boss, size);
    int saved_frame = frame;

    spawned = 0; checksum = 0;
    run(boss, &bridge, 120);
    int first_spawned = spawned; unsigned long long first = checksum;

    memcpy(boss, saved, size);  // 巻き戻す
    frame = saved_frame;
    spawned = 0; checksum = 0;
    run(boss, &bridge, 120);

    printf("state=%lld bytes, spawned in 120 frames: %d / after rewind: %d, same=%s\n",
           size, first_spawned, spawned, first == checksum ? "yes" : "no");
    tama_free(boss);
    free(saved);
    return first == checksum && first_spawned > 0 ? 0 : 1;
}
