// Tama のランタイム。コンパイラが出す LLVM IR から tama_rt_* として呼ばれる
#include <math.h>
#include "../../common/unibridge.h"

static const double TAU = 6.283185307179586;

static uint32_t to_rgba(int32_t rgb) { return ((uint32_t)rgb << 8) | 0xFF; }

double tama_rt_hp(UniBridge *bridge) { return bridge->hp_ratio(bridge->user); }

void tama_rt_ring(UniBridge *bridge, int32_t count, double speed, double offset, int32_t rgb) {
    for (int32_t i = 0; i < count; i++) {
        float angle = (float)(offset + TAU * i / count);
        bridge->spawn(0.0f, 0.0f, angle, (float)speed, 0.0f, to_rgba(rgb), bridge->user);
    }
}

void tama_rt_aim(UniBridge *bridge, double speed, int32_t rgb) {
    bridge->spawn(0.0f, 0.0f, bridge->aim_angle(bridge->user), (float)speed, 0.0f, to_rgba(rgb), bridge->user);
}

double tama_rt_ease_out(double t) { return 1.0 - (1.0 - t) * (1.0 - t); }

static double channel(int32_t rgb, int shift) { return ((rgb >> shift) & 0xFF) / 255.0; }

static int32_t pack(double r, double g, double b) {
    return ((int32_t)lround(r * 255) << 16) | ((int32_t)lround(g * 255) << 8) | (int32_t)lround(b * 255);
}

int32_t tama_rt_mix(int32_t a, int32_t b, double t) {
    double r = channel(a, 16) + (channel(b, 16) - channel(a, 16)) * t;
    double g = channel(a, 8) + (channel(b, 8) - channel(a, 8)) * t;
    double bl = channel(a, 0) + (channel(b, 0) - channel(a, 0)) * t;
    return pack(r, g, bl);
}

// 色相を angle（ラジアン）だけ回す。HSV に変換して戻す
int32_t tama_rt_hue(int32_t rgb, double angle) {
    double r = channel(rgb, 16), g = channel(rgb, 8), b = channel(rgb, 0);
    double max = fmax(r, fmax(g, b)), min = fmin(r, fmin(g, b)), delta = max - min;
    double h = 0.0;
    if (delta > 0.0) {
        if (max == r) h = fmod((g - b) / delta, 6.0);
        else if (max == g) h = (b - r) / delta + 2.0;
        else h = (r - g) / delta + 4.0;
        h /= 6.0;
    }
    double s = max > 0.0 ? delta / max : 0.0, v = max;
    h = fmod(h + angle / TAU, 1.0);
    if (h < 0.0) h += 1.0;
    double i = floor(h * 6.0), f = h * 6.0 - i;
    double p = v * (1 - s), q = v * (1 - f * s), u = v * (1 - (1 - f) * s);
    switch ((int)i % 6) {
        case 0: return pack(v, u, p);
        case 1: return pack(q, v, p);
        case 2: return pack(p, v, u);
        case 3: return pack(p, q, v);
        case 4: return pack(u, p, v);
        default: return pack(v, p, q);
    }
}
