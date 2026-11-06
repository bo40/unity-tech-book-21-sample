# ボス 5: Python ─ 弾で文字を描く。
# 弾がボスから飛び出し、空中でぴたりと止まって文字列を描く。
# api は C の組み込みモジュール unibridge（pybridge.c）。yield n で n フレーム待つ。
import colorsys
import math

CELL = 0.15          # 1 文字セルの大きさ（ワールド単位）
FPS = 60

BANNER = """
#  # ### #   #    ###
#  # #   #   #   #   #
#### ### #   #   #   #
#  # #   #   #   #   #
#  # ### ### ###  ###
"""


def draw_banner(api, arrive_in=60):
    dots = [(x, y) for y, line in enumerate(BANNER.strip("\n").splitlines())
                   for x, ch in enumerate(line) if ch == "#"]
    width = max(x for x, _ in dots)
    seconds = arrive_in / FPS
    for x, y in dots:
        dx, dy = (x - width / 2) * CELL, -1.0 - y * CELL
        distance = math.hypot(dx, dy)
        speed = 2 * distance / seconds              # T 秒後にちょうど止まる初速
        r, g, b = colorsys.hsv_to_rgb(x / width, 0.8, 1.0)
        api.spawn(angle=math.atan2(dy, dx), speed=speed, accel=-speed / seconds, rgb=(r, g, b))
    yield arrive_in + 60


def aimed_burst(api, shots=5):
    for _ in range(shots):
        center = api.aim()
        for spread in (-0.3, -0.15, 0.0, 0.15, 0.3):
            api.spawn(angle=center + spread, speed=4.0, rgb=(1.0, 0.9, 0.3))
        yield 8


def boss(api):
    while True:
        yield from draw_banner(api)
        yield from aimed_burst(api)
        yield 30
