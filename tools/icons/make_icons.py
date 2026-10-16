"""Generates every Hyland Skills icon as a flat two-tone vector and rasterises it to PNG.

Style: cream glyph + one accent colour per branch, thick dark outline so icons read on any node colour.
Run:  python -I tools/icons/make_icons.py  (writes HylandSkills/Assets/Icons/*.png)
"""
import os
import sys

import resvg_py

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "HylandSkills", "Assets", "Icons")
SIZE = 256

W = "#F3EEE2"   # cream glyph
D = "#15171B"   # outline
SH = "#C9C2B2"  # shaded cream
BR = {
    "cultivation": "#6FBF4A",
    "chemistry": "#3FA7D6",
    "empire": "#E0B33A",
    "shadows": "#8C6FD1",
    "hustle": "#F08A3C",
    "muscle": "#D9473F",
    "gold": "#E8C15A",
    "red": "#E2554B",
}

ICONS = {}


def icon(name, accent=None):
    def wrap(fn):
        ICONS[name] = (fn, accent)
        return fn
    return wrap


def svg(body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{SIZE}" height="{SIZE}" viewBox="0 0 256 256">'
            f'<g stroke="{D}" stroke-width="10" stroke-linejoin="round" stroke-linecap="round">{body}</g></svg>')


# ---------- primitives (outline comes from the group stroke) ----------
def circle(cx, cy, r, fill=W, sw=None):
    s = f' stroke-width="{sw}"' if sw is not None else ""
    return f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}"{s}/>'


def rect(x, y, w, h, rx=10, fill=W, sw=None):
    s = f' stroke-width="{sw}"' if sw is not None else ""
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}"{s}/>'


def path(d, fill=W, sw=None):
    s = f' stroke-width="{sw}"' if sw is not None else ""
    return f'<path d="{d}" fill="{fill}"{s}/>'


def line(x1, y1, x2, y2, sw=10, col=D):
    return f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{col}" stroke-width="{sw}"/>'


def poly(pts, fill=W, sw=None):
    s = f' stroke-width="{sw}"' if sw is not None else ""
    p = " ".join(f"{x},{y}" for x, y in pts)
    return f'<polygon points="{p}" fill="{fill}"{s}/>'


def text(x, y, t, size=60, fill=W, weight=900):
    return (f'<text x="{x}" y="{y}" font-family="Arial Black, Arial, sans-serif" font-size="{size}" font-weight="{weight}" '
            f'text-anchor="middle" fill="{fill}" stroke="{D}" stroke-width="8" paint-order="stroke">{t}</text>')


# ---------- reusable glyph parts ----------
def leaf(cx, cy, s=1.0, fill=None, rot=0):
    fill = fill or BR["cultivation"]
    d = f"M0,-60 C34,-40 38,10 0,60 C-38,10 -34,-40 0,-60 Z"
    vein = "M0,-44 L0,48"
    return (f'<g transform="translate({cx},{cy}) rotate({rot}) scale({s})">'
            f'{path(d, fill)}{path(vein, "none", 7)}</g>')


def weed_leaf(cx, cy, s=1.0, fill=None):
    fill = fill or BR["cultivation"]
    parts = []
    for ang, ln in ((0, 1.0), (-38, 0.85), (38, 0.85), (-72, 0.62), (72, 0.62)):
        parts.append(f'<g transform="rotate({ang})"><path d="M0,0 C{14*ln},{-30*ln} {12*ln},{-70*ln} 0,{-96*ln} '
                     f'C{-12*ln},{-70*ln} {-14*ln},{-30*ln} 0,0 Z" fill="{fill}"/></g>')
    stem = f'<path d="M0,0 L0,34" fill="none" stroke-width="10"/>'
    return f'<g transform="translate({cx},{cy}) scale({s})">{"".join(parts)}{stem}</g>'


def pot(cx, cy, s=1.0, fill="#B9764A"):
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M-46,-10 L46,-10 L36,50 L-36,50 Z", fill)}{rect(-54, -26, 108, 20, 6, fill)}</g>')


def flask(cx, cy, s=1.0, liquid=None):
    liquid = liquid or BR["chemistry"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M-18,-70 L18,-70 L18,-20 L58,52 C62,62 56,70 46,70 L-46,70 C-56,70 -62,62 -58,52 L-18,-20 Z", W)}'
            f'{path("M-40,30 L40,30 L52,54 C54,60 50,64 44,64 L-44,64 C-50,64 -54,60 -52,54 Z", liquid, 0)}'
            f'{rect(-26, -82, 52, 16, 6, SH)}</g>')


def coin(cx, cy, r=34, fill=None):
    fill = fill or BR["gold"]
    return circle(cx, cy, r, fill) + f'<circle cx="{cx}" cy="{cy}" r="{r*0.6}" fill="none" stroke-width="6"/>'


def bill(cx, cy, s=1.0, rot=0):
    return (f'<g transform="translate({cx},{cy}) rotate({rot}) scale({s})">{rect(-62, -34, 124, 68, 8, "#8DC26F")}'
            f'{circle(0, 0, 18, "#5E9A45", 7)}</g>')


def eye(cx, cy, s=1.0, closed=False):
    if closed:
        return (f'<g transform="translate({cx},{cy}) scale({s})">'
                f'{path("M-70,0 C-40,36 40,36 70,0", "none")}'
                f'{line(-44, 22, -54, 40)}{line(0, 30, 0, 50)}{line(44, 22, 54, 40)}</g>')
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M-74,0 C-40,-46 40,-46 74,0 C40,46 -40,46 -74,0 Z", W)}{circle(0, 0, 24, BR["shadows"])}{circle(0, 0, 9, D, 0)}</g>')


def badge_police(cx, cy, s=1.0):
    pts = []
    import math
    for i in range(14):
        r = 62 if i % 2 == 0 else 44
        a = math.pi * 2 * i / 14 - math.pi / 2
        pts.append((round(r * math.cos(a), 1), round(r * math.sin(a), 1)))
    return (f'<g transform="translate({cx},{cy}) scale({s})">{poly(pts, "#5C7FB8")}{circle(0, 0, 22, BR["gold"], 7)}</g>')


def fist(cx, cy, s=1.0, fill=W):
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{rect(-52, -40, 104, 84, 22, fill)}'
            f'{line(-26, -40, -26, -6, 7)}{line(0, -40, 0, -6, 7)}{line(26, -40, 26, -6, 7)}'
            f'{path("M-52,10 C-70,6 -72,-26 -50,-28", fill)}</g>')


def heart(cx, cy, s=1.0, fill=None):
    fill = fill or BR["muscle"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M0,56 C-70,6 -74,-50 -32,-56 C-12,-58 0,-42 0,-30 C0,-42 12,-58 32,-56 C74,-50 70,6 0,56 Z", fill)}</g>')


def clock(cx, cy, r=60, fill=W):
    return circle(cx, cy, r, fill) + line(cx, cy, cx, cy - r * 0.55, 9) + line(cx, cy, cx + r * 0.42, cy + r * 0.2, 9)


def moon(cx, cy, s=1.0, fill=None):
    fill = fill or BR["shadows"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M20,-62 C-30,-58 -56,-6 -34,36 C-14,72 40,74 64,40 C24,50 -12,20 -6,-18 C-2,-42 6,-54 20,-62 Z", fill)}</g>')


def chat(cx, cy, s=1.0, fill=None):
    fill = fill or BR["hustle"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M-70,-50 L70,-50 C80,-50 84,-46 84,-36 L84,22 C84,32 80,36 70,36 L-10,36 L-44,62 L-38,36 L-70,36 C-80,36 -84,32 -84,22 L-84,-36 C-84,-46 -80,-50 -70,-50 Z", fill)}'
            f'{circle(-36, -7, 9, W, 0)}{circle(0, -7, 9, W, 0)}{circle(36, -7, 9, W, 0)}</g>')


def phone(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{rect(-40, -70, 80, 140, 16, "#2B2F37")}'
            f'{rect(-30, -56, 60, 98, 6, BR["hustle"], 0)}{circle(0, 56, 6, W, 0)}</g>')


def bolt(cx, cy, s=1.0, fill=None):
    fill = fill or BR["gold"]
    return f'<g transform="translate({cx},{cy}) scale({s})">{poly([(10,-70),(-40,8),(-6,8),(-16,70),(40,-12),(6,-12)], fill)}</g>'


def drop(cx, cy, s=1.0, fill="#4AA8E0"):
    return f'<g transform="translate({cx},{cy}) scale({s})">{path("M0,-56 C20,-26 40,0 40,22 C40,46 22,60 0,60 C-22,60 -40,46 -40,22 C-40,0 -20,-26 0,-56 Z", fill)}</g>'


def arrow_up(cx, cy, s=1.0, fill=None):
    fill = fill or BR["gold"]
    return f'<g transform="translate({cx},{cy}) scale({s})">{poly([(0,-40),(34,0),(14,0),(14,36),(-14,36),(-14,0),(-34,0)], fill)}</g>'


def arrow_down(cx, cy, s=1.0, fill=None):
    fill = fill or BR["red"]
    return f'<g transform="translate({cx},{cy}) scale({s})">{poly([(0,40),(34,0),(14,0),(14,-36),(-14,-36),(-14,0),(-34,0)], fill)}</g>'


def star(cx, cy, r=40, fill=None):
    import math
    fill = fill or BR["gold"]
    pts = []
    for i in range(10):
        rr = r if i % 2 == 0 else r * 0.45
        a = math.pi * 2 * i / 10 - math.pi / 2
        pts.append((round(cx + rr * math.cos(a), 1), round(cy + rr * math.sin(a), 1)))
    return poly(pts, fill)


def building(cx, cy, s=1.0, fill=W):
    win = "".join(rect(-34 + c * 26, -50 + r * 30, 16, 18, 3, BR["empire"], 0) for r in range(3) for c in range(3))
    return f'<g transform="translate({cx},{cy}) scale({s})">{rect(-52, -70, 104, 140, 6, fill)}{win}{rect(-14, 36, 28, 34, 3, "#2B2F37", 6)}</g>'


def gear(cx, cy, r=44, fill=W, teeth=8):
    import math
    pts = []
    inner = r * 0.76
    for i in range(teeth):
        a0 = math.pi * 2 * i / teeth
        a1 = math.pi * 2 * (i + 1) / teeth
        w = (a1 - a0)
        for rr, a in ((inner, a0), (r, a0 + w * 0.18), (r, a0 + w * 0.5), (inner, a0 + w * 0.68)):
            pts.append((round(cx + rr * math.cos(a), 1), round(cy + rr * math.sin(a), 1)))
    return poly(pts, fill) + circle(cx, cy, r * 0.3, D, 0)


def shield(cx, cy, s=1.0, fill=None):
    fill = fill or BR["muscle"]
    return f'<g transform="translate({cx},{cy}) scale({s})">{path("M0,-70 L60,-48 C60,10 40,50 0,72 C-40,50 -60,10 -60,-48 Z", fill)}</g>'


def gun(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M-80,-30 L70,-30 L70,0 L-10,0 L-22,52 L-56,52 L-44,0 L-80,0 Z", "#3A3F48")}'
            f'{rect(46, -44, 14, 16, 3, "#3A3F48", 7)}</g>')


def crosshair(cx, cy, r=46, col=None):
    col = col or BR["red"]
    return (f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{col}" stroke-width="10"/>'
            f'{line(cx, cy - r - 16, cx, cy - r + 16, 10, col)}{line(cx, cy + r - 16, cx, cy + r + 16, 10, col)}'
            f'{line(cx - r - 16, cy, cx - r + 16, cy, 10, col)}{line(cx + r - 16, cy, cx + r + 16, cy, 10, col)}')


def truck(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{rect(-80, -40, 96, 66, 6, W)}'
            f'{path("M16,-20 L52,-20 L76,6 L76,26 L16,26 Z", BR["empire"])}{circle(-50, 34, 16, "#3A3F48")}{circle(46, 34, 16, "#3A3F48")}</g>')


def skull(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{path("M-50,-10 C-50,-60 50,-60 50,-10 C50,14 38,24 30,28 L30,48 L-30,48 L-30,28 C-38,24 -50,14 -50,-10 Z", W)}'
            f'{circle(-20, -6, 13, D, 0)}{circle(20, -6, 13, D, 0)}</g>')


def mushroom(cx, cy, s=1.0, cap=None):
    cap = cap or "#C45A3C"
    return (f'<g transform="translate({cx},{cy}) scale({s})">{rect(-16, -6, 32, 62, 12, W)}'
            f'{path("M-66,0 C-66,-60 66,-60 66,0 Z", cap)}{circle(-26, -24, 8, W, 0)}{circle(18, -34, 7, W, 0)}{circle(36, -12, 6, W, 0)}</g>')


def scissors(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{line(-40, -60, 28, 30, 12)}{line(40, -60, -28, 30, 12)}'
            f'{circle(-34, 44, 18, BR["cultivation"])}{circle(34, 44, 18, BR["cultivation"])}</g>')


def seed(cx, cy, s=1.0):
    return f'<g transform="translate({cx},{cy}) scale({s})">{path("M0,-46 C30,-30 34,24 0,46 C-34,24 -30,-30 0,-46 Z", "#B98A55")}{path("M0,-30 C10,-6 10,10 0,30", "none", 6)}</g>'


def bulb(cx, cy, s=1.0, fill=None):
    fill = fill or BR["gold"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">{path("M0,-62 C36,-62 56,-36 56,-8 C56,16 36,28 30,46 L-30,46 C-36,28 -56,16 -56,-8 C-56,-36 -36,-62 0,-62 Z", fill)}'
            f'{rect(-26, 46, 52, 22, 6, "#8A8F98")}</g>')


def diamond_gem(cx, cy, s=1.0, fill=None):
    fill = fill or BR["chemistry"]
    return f'<g transform="translate({cx},{cy}) scale({s})">{poly([(-50,-20),(-26,-48),(26,-48),(50,-20),(0,50)], fill)}{path("M-50,-20 L50,-20 M-14,-48 L0,-20 L14,-48 M0,-20 L0,50", "none", 6)}</g>'


def scale_icon(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{line(0, -60, 0, 52)}{line(-60, -40, 60, -40)}'
            f'{path("M-86,10 L-34,10 C-38,34 -82,34 -86,10 Z", BR["gold"])}{path("M34,10 L86,10 C82,34 38,34 34,10 Z", BR["red"])}'
            f'{line(-60, -40, -80, 10, 6)}{line(-60, -40, -40, 10, 6)}{line(60, -40, 40, 10, 6)}{line(60, -40, 80, 10, 6)}'
            f'{rect(-36, 52, 72, 16, 6, W)}</g>')


def ghost(cx, cy, s=1.0, fill=W):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{path("M-50,60 L-50,-10 C-50,-74 50,-74 50,-10 L50,60 L30,44 L12,60 L-6,44 L-24,60 Z", fill)}'
            f'{circle(-18, -14, 10, D, 0)}{circle(18, -14, 10, D, 0)}</g>')


def car(cx, cy, s=1.0, fill=None):
    fill = fill or BR["shadows"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">{path("M-84,18 L-84,-4 C-84,-14 -78,-18 -68,-20 L-40,-24 L-18,-50 L36,-50 L60,-22 C76,-18 84,-12 84,0 L84,18 Z", fill)}'
            f'{path("M-10,-40 L30,-40 L46,-22 L-26,-22 Z", "#BFE3F5", 6)}{circle(-48, 22, 18, "#3A3F48")}{circle(48, 22, 18, "#3A3F48")}</g>')


def handshake(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">'
            f'{path("M-90,-10 L-50,-30 L-10,-14 L30,-34 L90,-10 L90,20 L56,24 L20,50 C10,58 -2,56 -10,48 L-60,20 L-90,20 Z", BR["hustle"])}'
            f'{path("M-10,-14 L-30,6 C-36,14 -26,24 -16,18 L10,0", W, 8)}</g>')


def dealer(cx, cy, s=1.0, fill=None):
    fill = fill or BR["hustle"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">{circle(0, -34, 28, W)}'
            f'{path("M-56,62 C-56,10 -30,-2 0,-2 C30,-2 56,10 56,62 Z", fill)}'
            f'{path("M-34,-46 L34,-46 L26,-62 L-26,-62 Z", "#3A3F48", 7)}</g>')


def bag(cx, cy, s=1.0, fill=None):
    fill = fill or BR["cultivation"]
    return (f'<g transform="translate({cx},{cy}) scale({s})">{rect(-44, -40, 88, 96, 10, "#E9F2E2")}'
            f'{rect(-44, -52, 88, 14, 4, "#3A3F48")}{circle(0, 12, 22, fill)}</g>')


def flame(cx, cy, s=1.0):
    return f'<g transform="translate({cx},{cy}) scale({s})">{path("M0,-70 C30,-36 52,-14 52,18 C52,48 28,66 0,66 C-28,66 -52,48 -52,18 C-52,-6 -36,-22 -26,-34 C-22,-14 -12,-4 -2,-4 C-10,-30 -6,-50 0,-70 Z", "#F08A3C")}{path("M0,4 C14,20 24,30 24,44 C24,58 12,64 0,64 C-12,64 -24,58 -24,44 C-24,32 -12,22 0,4 Z", BR["gold"], 6)}</g>'


def soil(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{path("M-80,20 C-60,-10 60,-10 80,20 L80,50 L-80,50 Z", "#7A5235")}'
            f'{circle(-40, 22, 6, "#A97A52", 0)}{circle(10, 30, 6, "#A97A52", 0)}{circle(46, 18, 6, "#A97A52", 0)}</g>')


def cash_stack(cx, cy, s=1.0):
    return (f'<g transform="translate({cx},{cy}) scale({s})">{bill(0, 26)}{bill(0, 0)}{bill(0, -26)}</g>')


# =====================================================================
# BRANCHES + UI
# =====================================================================
@icon("branch_cultivation")
def _():
    return weed_leaf(128, 150, 1.05, W)


@icon("branch_chemistry")
def _():
    return flask(128, 132, 1.25, W)


@icon("branch_empire")
def _():
    return building(128, 128, 1.15)


@icon("branch_shadows")
def _():
    return path("M128,30 C70,30 52,90 52,150 C52,196 70,226 128,226 C186,226 204,196 204,150 C204,90 186,30 128,30 Z", W) + \
        path("M84,150 C84,112 104,92 128,92 C152,92 172,112 172,150 C172,180 152,196 128,196 C104,196 84,180 84,150 Z", D, 0) + \
        circle(108, 146, 9, W, 0) + circle(148, 146, 9, W, 0)


@icon("branch_hustle")
def _():
    return handshake(128, 132, 1.15).replace(BR["hustle"], W)


@icon("branch_muscle")
def _():
    return fist(128, 130, 1.4)


@icon("app_icon")
def _():
    # Native phone icons: full-bleed colour with a soft vertical gradient and a flat white glyph (no outline).
    grad = ('<defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1">'
            '<stop offset="0" stop-color="#2FB596"/><stop offset="1" stop-color="#1A7A66"/></linearGradient></defs>')
    bg = '<rect x="0" y="0" width="256" height="256" fill="url(#g)" stroke="none"/>'
    pts = [(128, 54), (192, 91), (192, 165), (128, 202), (64, 165), (64, 91)]
    ring = '<polygon points="' + " ".join(f"{x},{y}" for x, y in pts) + '" fill="none" stroke="#FFFFFF" stroke-width="9" stroke-linejoin="round"/>'
    spokes = "".join(f'<line x1="128" y1="128" x2="{x}" y2="{y}" stroke="#FFFFFF" stroke-opacity="0.55" stroke-width="6"/>' for x, y in pts)
    nodes = "".join(f'<circle cx="{x}" cy="{y}" r="17" fill="#FFFFFF" stroke="none"/>' for x, y in pts)
    core = '<circle cx="128" cy="128" r="27" fill="#FFFFFF" stroke="none"/>' + star(128, 128, 17, "#1A7A66").replace('/>', ' stroke="none"/>')
    return grad + bg + ring + spokes + nodes + core


@icon("ui_settings")
def _():
    return gear(128, 128, 92)


@icon("ui_respec")
def _():
    return (f'<path d="M64,128 A64,64 0 1 1 92,182" fill="none" stroke="{W}" stroke-width="22"/>'
            + poly([(40, 120), (90, 120), (64, 160)], W, 0))


@icon("ui_police")
def _():
    return badge_police(128, 128, 1.6).replace(f'stroke="{D}"', 'stroke="#FFFFFF"')


@icon("ui_tradeoff")
def _():
    return arrow_up(92, 128, 1.4, W) + arrow_down(164, 128, 1.4, W)


# =====================================================================
# CULTIVATION
# =====================================================================
@icon("green_thumb")
def _():
    return pot(128, 186, 1.1) + weed_leaf(128, 150, 0.95) + arrow_up(196, 70, 0.9)


@icon("deep_roots")
def _():
    roots = path("M128,120 C120,160 96,170 76,214 M128,120 C128,170 132,190 128,226 M128,120 C140,160 166,172 182,212", "none", 10)
    return soil(128, 112, 1.2) + roots + leaf(128, 64, 0.75) + drop(206, 70, 0.55)


@icon("seed_saver")
def _():
    return (f'<path d="M64,150 A70,70 0 0 1 180,82" fill="none" stroke="{W}" stroke-width="18"/>'
            + poly([(170, 54), (204, 92), (156, 104)], W, 0) + seed(132, 152, 1.25))


@icon("green_sense")
def _():
    return weed_leaf(116, 158, 0.95) + star(186, 74, 40)


@icon("organic_methods")
def _():
    return leaf(108, 132, 1.3, rot=-20) + circle(186, 178, 40, BR["red"]) + line(160, 152, 212, 204, 12, W) + text(186, 98, "", 10)


@icon("prune")
def _():
    return weed_leaf(100, 176, 0.8) + scissors(176, 104, 0.9)


@icon("grow_overdrive")
def _():
    return bulb(128, 98, 1.1) + bolt(128, 92, 0.55, W) + pot(128, 210, 0.7) + weed_leaf(128, 196, 0.45)


@icon("living_soil")
def _():
    return soil(128, 150, 1.35) + leaf(96, 118, 0.6, rot=-30) + leaf(160, 112, 0.7, rot=25) + \
        f'<path d="M70,70 A58,58 0 0 1 186,70" fill="none" stroke="{BR["gold"]}" stroke-width="14"/>'


@icon("heirloom_genetics")
def _():
    helix = path("M92,40 C164,80 92,120 164,160 C200,180 180,210 164,220", "none", 12) + \
        path("M164,40 C92,80 164,120 92,160 C56,180 76,210 92,220", "none", 12)
    rungs = "".join(line(104, y, 152, y, 7, BR["cultivation"]) for y in (60, 100, 140, 180))
    return helix.replace(f'fill="none"', f'fill="none" stroke="{W}"') + rungs + star(196, 64, 30)


@icon("mycologist")
def _():
    return mushroom(104, 150, 1.0) + mushroom(180, 176, 0.65, "#D98A3C")


@icon("head_botanist")
def _():
    return dealer(128, 150, 1.2, BR["cultivation"]) + leaf(186, 74, 0.6, rot=30)


# =====================================================================
# CHEMISTRY
# =====================================================================
@icon("steady_hands")
def _():
    return flask(110, 140, 1.05) + clock(190, 76, 40)


@icon("precise_measures")
def _():
    marks = "".join(line(100, y, 124, y, 6) for y in (80, 110, 140, 170))
    return rect(84, 40, 64, 180, 14, W) + rect(92, 120, 48, 92, 8, BR["chemistry"], 0) + marks + star(190, 80, 34)


@icon("mixologist")
def _():
    bowl = path("M40,110 L216,110 C210,180 170,214 128,214 C86,214 46,180 40,110 Z", W)
    liquid = path("M56,128 L200,128 C192,172 162,198 128,198 C94,198 64,172 56,128 Z", BR["chemistry"], 0)
    swirl = path("M88,150 C108,130 148,170 168,150", "none", 8)
    return bowl + liquid + swirl + line(150, 40, 120, 140, 14)


@icon("ingredient_saver")
def _():
    return (f'<path d="M58,150 A72,72 0 1 1 112,204" fill="none" stroke="{W}" stroke-width="18"/>'
            + poly([(36, 140), (86, 140), (60, 182)], W, 0) + rect(104, 96, 52, 72, 12, BR["chemistry"]) + rect(112, 82, 36, 18, 5, W))


@icon("volatile_chemistry")
def _():
    return flask(108, 150, 0.95) + flame(188, 80, 0.75)


@icon("lab_focus")
def _():
    return eye(128, 110, 1.3).replace(BR["shadows"], BR["chemistry"]) + diamond_gem(128, 196, 0.6)


@icon("overclock")
def _():
    return gear(108, 140, 70) + bolt(186, 82, 0.8)


@icon("complex_formulas")
def _():
    a = circle(128, 72, 30, BR["chemistry"]) + circle(68, 176, 30, BR["hustle"]) + circle(188, 176, 30, BR["cultivation"])
    links = line(128, 72, 68, 176, 12, W) + line(128, 72, 188, 176, 12, W) + line(68, 176, 188, 176, 12, W)
    return f'<g stroke="{D}">{line(128, 72, 68, 176, 22)}{line(128, 72, 188, 176, 22)}{line(68, 176, 188, 176, 22)}</g>' + links + a


@icon("crystal_purity")
def _():
    return diamond_gem(128, 136, 1.7) + star(200, 60, 26, W)


@icon("alchemist")
def _():
    return flask(128, 140, 1.1, BR["gold"]) + star(196, 66, 26, W) + star(60, 72, 18, W)


@icon("coca_refiner")
def _():
    pot_ = path("M44,96 L212,96 L196,206 C194,214 188,218 180,218 L76,218 C68,218 62,214 60,206 Z", "#3A3F48")
    return pot_ + rect(32, 82, 192, 22, 8, "#5A606B") + leaf(100, 60, 0.6, rot=-30) + leaf(156, 56, 0.6, rot=30) + \
        path("M80,140 L176,140", "none", 0) + circle(128, 160, 26, W)


# =====================================================================
# EMPIRE
# =====================================================================
@icon("bookkeeper")
def _():
    book = rect(54, 44, 148, 172, 10, "#2B5C3A") + rect(70, 44, 132, 172, 6, W)
    lines_ = "".join(line(92, y, 182, y, 6) for y in (82, 112, 142))
    return book + lines_ + coin(176, 182, 30)


@icon("bulk_buyer")
def _():
    boxes = rect(48, 126, 76, 76, 8, "#C9965A") + rect(132, 126, 76, 76, 8, "#C9965A") + rect(90, 50, 76, 76, 8, "#C9965A")
    tape = line(86, 126, 86, 202, 6) + line(170, 126, 170, 202, 6) + line(128, 50, 128, 126, 6)
    return boxes + tape + circle(198, 70, 32, BR["red"]) + text(198, 84, "%", 40)


@icon("payroll")
def _():
    return dealer(110, 140, 1.15, BR["empire"]) + bill(182, 186, 0.6, -15)


@icon("atm_connections")
def _():
    atm = rect(56, 36, 144, 188, 14, "#4A505C") + rect(76, 58, 104, 64, 6, "#BFE3F5", 6) + rect(86, 144, 84, 14, 5, D, 0)
    return atm + bill(128, 188, 0.7) + arrow_up(206, 78, 0.6)


@icon("overtime_culture")
def _():
    return dealer(104, 150, 1.05, BR["empire"]) + clock(184, 84, 44)


@icon("rush_delivery")
def _():
    speed = line(20, 120, 50, 120, 9, W) + line(10, 146, 44, 146, 9, W)
    return truck(140, 132, 1.1) + speed


@icon("cook_the_books")
def _():
    return rect(54, 70, 148, 150, 10, W) + "".join(line(78, y, 178, y, 6) for y in (104, 134, 164)) + flame(128, 92, 0.6)


@icon("legit_fronts")
def _():
    return building(104, 140, 0.95) + rect(156, 120, 80, 50, 10, "#2B5C3A") + text(196, 158, "OPEN", 22)


@icon("tycoon")
def _():
    crown = poly([(44, 150), (60, 70), (100, 120), (128, 56), (156, 120), (196, 70), (212, 150)], BR["gold"])
    return crown + rect(44, 150, 168, 34, 8, BR["gold"]) + circle(128, 166, 10, BR["red"], 6) + cash_stack(128, 214, 0.5)


@icon("union_boss")
def _():
    return dealer(80, 150, 0.8, BR["empire"]) + dealer(176, 150, 0.8, BR["empire"]) + dealer(128, 130, 1.0, BR["gold"])


@icon("offshore_account")
def _():
    sea = path("M20,176 C50,160 70,192 100,176 C130,160 150,192 180,176 C210,160 226,184 236,176 L236,236 L20,236 Z", "#3C7FB5")
    palm = path("M118,170 C114,130 120,96 132,70", "none", 12) + leaf(110, 70, 0.55, rot=-70) + leaf(156, 70, 0.55, rot=70) + leaf(132, 54, 0.5)
    return sea + palm + coin(196, 80, 30)


# =====================================================================
# SHADOWS
# =====================================================================
@icon("low_profile")
def _():
    hood = path("M128,30 C70,30 46,84 46,140 L46,226 L210,226 L210,140 C210,84 186,30 128,30 Z", "#4A4F5C")
    face = path("M84,150 C84,108 104,86 128,86 C152,86 172,108 172,150 C172,182 152,200 128,200 C104,200 84,182 84,150 Z", D, 0)
    eyes = rect(98, 136, 24, 8, 4, W, 0) + rect(134, 136, 24, 8, 4, W, 0)
    return hood + face + eyes


@icon("hidden_pockets")
def _():
    jacket = path("M48,40 L208,40 L208,220 L48,220 Z", "#3E4552") + path("M128,40 L128,220", "none", 8)
    pocket = rect(70, 120, 48, 50, 6, "#2F343E") + bag(176, 136, 0.55)
    return jacket + pocket + line(150, 110, 210, 110, 8, W)


@icon("light_steps")
def _():
    def shoe(x, y, rot):
        return (f'<g transform="translate({x},{y}) rotate({rot})">'
                f'{path("M0,-46 C20,-46 26,-20 22,0 C18,16 14,22 0,22 C-14,22 -18,16 -22,0 C-26,-20 -20,-46 0,-46 Z", W)}'
                f'{rect(-16, 30, 32, 26, 12, W)}</g>')
    waves = f'<path d="M40,214 C80,196 176,196 216,214" fill="none" stroke="{BR["shadows"]}" stroke-width="12"/>'
    return shoe(92, 120, -12) + shoe(164, 84, 12) + waves


@icon("night_owl")
def _():
    owl = path("M60,214 L60,110 C60,60 196,60 196,110 L196,214 Z", "#6A5A8E") + circle(96, 116, 26, W) + circle(160, 116, 26, W)
    eyes = circle(96, 116, 10, D, 0) + circle(160, 116, 10, D, 0) + poly([(118, 140), (138, 140), (128, 158)], BR["gold"], 6)
    return moon(196, 58, 0.5, BR["gold"]) + owl + eyes


@icon("clean_hands")
def _():
    hand = path("M70,220 L70,120 C70,110 84,110 84,120 L84,80 C84,70 98,70 98,80 L98,70 C98,60 112,60 112,70 L112,80 C112,70 126,70 126,80 L126,130 C136,116 152,120 146,136 L126,190 C120,206 108,220 92,220 Z", W)
    return hand + "".join(circle(x, y, r, "#BFE3F5", 6) for x, y, r in ((170, 70, 18), (196, 112, 12), (168, 140, 10)))


@icon("police_scanner")
def _():
    radio = rect(80, 70, 96, 150, 14, "#3A3F48") + rect(94, 92, 68, 40, 6, "#7FD3A5", 6) + line(150, 70, 166, 26, 10)
    waves = (f'<path d="M190,60 C210,80 210,110 190,130" fill="none" stroke="{BR["red"]}" stroke-width="10"/>'
             f'<path d="M66,60 C46,80 46,110 66,130" fill="none" stroke="{BR["red"]}" stroke-width="10"/>')
    return radio + waves + circle(110, 170, 10, D, 0) + circle(146, 170, 10, D, 0)


@icon("vanish")
def _():
    return ghost(128, 132, 1.25, W) + "".join(circle(x, y, r, BR["shadows"], 0) for x, y, r in ((46, 200, 10), (214, 190, 12), (206, 64, 8), (52, 70, 7)))


@icon("ghost_protocol")
def _():
    return ghost(108, 140, 1.0, W) + moon(190, 74, 0.55) + "".join(circle(x, y, 6, W, 0) for x, y in ((214, 130), (170, 150)))


@icon("phantom")
def _():
    return ghost(128, 136, 1.3, BR["shadows"]) + star(196, 56, 26)


@icon("night_king")
def _():
    crown = poly([(64, 170), (76, 104), (104, 140), (128, 90), (152, 140), (180, 104), (192, 170)], BR["gold"])
    return moon(128, 70, 0.65) + crown + rect(64, 170, 128, 28, 8, BR["gold"])


@icon("wheelman")
def _():
    wheel = circle(128, 128, 84, "#3A3F48") + circle(128, 128, 56, "none", 14) + circle(128, 128, 16, W)
    spokes = line(128, 112, 128, 72, 12, W) + line(114, 136, 82, 160, 12, W) + line(142, 136, 174, 160, 12, W)
    return wheel.replace('fill="none"', f'fill="none" stroke="{W}"') + spokes


# =====================================================================
# HUSTLE
# =====================================================================
@icon("silver_tongue")
def _():
    return chat(118, 120, 1.0, "#B9C0CB") + coin(196, 186, 32)


@icon("free_taste")
def _():
    return bag(110, 140, 1.15, BR["cultivation"]) + circle(186, 82, 34, BR["hustle"]) + text(186, 98, "0", 44)


@icon("people_person")
def _():
    return heart(128, 128, 1.45, BR["hustle"])


@icon("dealer_network")
def _():
    a = dealer(128, 92, 0.65, BR["hustle"]) + dealer(64, 186, 0.55, BR["hustle"]) + dealer(192, 186, 0.55, BR["hustle"])
    return line(128, 110, 64, 180, 10, W) + line(128, 110, 192, 180, 10, W) + a


@icon("premium_brand")
def _():
    tag = path("M54,70 L150,70 L210,130 L150,190 L54,190 Z", BR["gold"]) + circle(82, 130, 12, D, 0)
    return tag + star(148, 130, 30, W)


@icon("hype_text")
def _():
    return phone(110, 134, 1.2) + "".join(circle(x, y, 18, BR["red"]) for x, y in ((176, 68),)) + text(176, 82, "!", 38)


@icon("rush_hour")
def _():
    return clock(112, 140, 72) + bolt(192, 78, 0.7)


@icon("addiction_economics")
def _():
    return heart(108, 140, 1.05, BR["red"]) + path("M150,96 L200,96 L200,146", "none", 12) + arrow_up(200, 90, 0.7, BR["gold"])


@icon("street_legend")
def _():
    return star(128, 128, 96, BR["hustle"]) + star(128, 128, 44, W)


@icon("kingmaker")
def _():
    crown = poly([(70, 110), (82, 54), (106, 86), (128, 44), (150, 86), (174, 54), (186, 110)], BR["gold"])
    return crown + rect(70, 108, 116, 22, 6, BR["gold"]) + dealer(128, 186, 0.75, BR["hustle"])


@icon("vip_host")
def _():
    card = rect(40, 70, 176, 116, 16, "#2B2F37") + text(128, 148, "VIP", 56, BR["gold"])
    return card + star(208, 62, 26)


# =====================================================================
# MUSCLE
# =====================================================================
@icon("thick_skin")
def _():
    return shield(128, 130, 1.4, "#8A8F98") + heart(128, 130, 0.62)


@icon("heavy_hands")
def _():
    return fist(118, 140, 1.2) + "".join(line(x1, y1, x2, y2, 10, BR["gold"]) for x1, y1, x2, y2 in ((186, 60, 210, 36), (200, 96, 232, 92), (170, 40, 172, 14)))


@icon("steady_aim")
def _():
    return crosshair(128, 128, 64) + circle(128, 128, 12, BR["red"], 0)


@icon("second_wind")
def _():
    return heart(118, 140, 1.15) + f'<path d="M168,52 C206,62 216,96 200,124" fill="none" stroke="{W}" stroke-width="14"/>' + poly([(184, 120), (214, 136), (212, 104)], W, 0)


@icon("bruiser_build")
def _():
    return shield(110, 134, 1.2, "#8A8F98") + arrow_down(196, 170, 0.75)


@icon("adrenaline")
def _():
    return heart(118, 140, 1.15) + bolt(186, 80, 0.75)


@icon("intimidate")
def _():
    return skull(128, 134, 1.45)


@icon("notorious")
def _():
    poster = rect(56, 36, 144, 184, 8, "#E5D6B4") + text(128, 84, "WANTED", 30, D, 900).replace(f'stroke="{D}"', 'stroke="none"')
    return poster + circle(128, 140, 34, "#8A6E57") + line(76, 194, 180, 194, 8)


@icon("juggernaut")
def _():
    return shield(128, 128, 1.5, BR["muscle"]) + star(128, 124, 40, W)


@icon("gunslinger")
def _():
    return gun(128, 136, 1.1) + star(200, 60, 26)


@icon("protection_racket")
def _():
    return shield(128, 130, 1.4, BR["muscle"]) + dealer(128, 140, 0.6, W)


# =====================================================================
# SYNERGIES
# =====================================================================
@icon("syn_farm_to_lab")
def _():
    return weed_leaf(92, 160, 0.7) + flask(170, 128, 0.7)


@icon("syn_industrial_scale")
def _():
    return gear(98, 140, 54) + gear(170, 100, 40)


@icon("syn_offshore_accounts")
def _():
    return coin(96, 140, 44) + ghost(170, 120, 0.7)


@icon("syn_back_alley")
def _():
    return moon(96, 110, 0.8) + bag(170, 150, 0.7, BR["hustle"])


@icon("syn_respect")
def _():
    return fist(100, 140, 0.8) + handshake(170, 120, 0.55)


@icon("syn_home_turf")
def _():
    house = poly([(128, 40), (220, 120), (196, 120), (196, 214), (60, 214), (60, 120), (36, 120)], W)
    return house + shield(128, 160, 0.6, BR["muscle"])


def main():
    os.makedirs(OUT, exist_ok=True)
    only = set(sys.argv[1:])
    n = 0
    for name, (fn, _acc) in ICONS.items():
        if only and name not in only:
            continue
        data = resvg_py.svg_to_bytes(svg_string=svg(fn()), width=SIZE, height=SIZE)
        with open(os.path.join(OUT, name + ".png"), "wb") as f:
            f.write(bytes(data))
        n += 1
    print(f"wrote {n} icons to {os.path.abspath(OUT)}")


if __name__ == "__main__":
    main()
