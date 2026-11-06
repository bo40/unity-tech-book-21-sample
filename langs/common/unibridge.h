// unibridge.h — すべての言語のボスが見る、唯一の Unity 窓口（C ABI）
//
// 座標はボスからの相対位置（ワールド単位）。角度はラジアン（0 が右、反時計回り）。
// speed は毎秒進むワールド単位、accel は毎秒の速さの増減（負なら減速して 0 で止まる）。
// 色は 0xRRGGBBAA。
#pragma once
#include <stdint.h>

typedef struct UniBridge {
    void (*spawn)(float x, float y, float angle, float speed, float accel, uint32_t rgba, void *user);
    float (*hp_ratio)(void *user);   // ボスの残り HP の割合（1.0 → 0.0）
    float (*aim_angle)(void *user);  // ボスから見た自機の方向
    void *user;                      // Unity 側の持ち物。ボスは中身を触らない
} UniBridge;
