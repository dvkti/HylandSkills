"""Minimalist skill icons: Lucide line icons (ISC license) rendered as white strokes on transparency.

The mod tints them at runtime (branch colour, locked grey...), so one white icon serves every state.
Usage: python tools/icons/make_icons_v2.py <path to lucide-static/icons>
Writes HylandSkills/Assets/Icons/<name>.png (keeps the hand-made app_icon.png).
"""
import glob
import os
import re
import sys

import resvg_py

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "..", "HylandSkills", "Assets", "Icons")
DEFS = os.path.join(HERE, "..", "..", "HylandSkills", "src", "Data", "SkillDefs.cs")
SIZE = 128
STROKE = 2.0

# Icons referenced outside SkillDefs (branch tiles and app UI) -> Lucide name
EXTRA = {
    "branch_cultivation": "cannabis",
    "branch_chemistry": "flask-conical",
    "branch_empire": "building-2",
    "branch_gambling": "dices",
    "branch_hustle": "handshake",
    "branch_shadows": "glasses",
    "branch_mobility": "person-standing",
    "branch_muscle": "biceps-flexed",
    "ui_settings": "settings",
    "ui_respec": "rotate-ccw",
    "ui_tradeoff": "arrow-down-up",
    "ui_police": "shield-alert",
    "ui_back": "arrow-left",
    "ui_lock": "lock",
    "ui_combo": "link",
    "ui_check": "check",
    "ui_close": "x",
    "diamond": "diamond",
}


def render(src_svg, out_png):
    svg = open(src_svg, encoding="utf-8").read()
    svg = re.sub(r"<!--.*?-->", "", svg, flags=re.S)
    svg = svg.replace('stroke="currentColor"', 'stroke="#FFFFFF"')
    svg = re.sub(r'stroke-width="[\d.]+"', f'stroke-width="{STROKE}"', svg)
    svg = re.sub(r'width="24"', f'width="{SIZE}"', svg, count=1)
    svg = re.sub(r'height="24"', f'height="{SIZE}"', svg, count=1)
    data = resvg_py.svg_to_bytes(svg_string=svg, width=SIZE, height=SIZE)
    with open(out_png, "wb") as f:
        f.write(bytes(data))


def main():
    lucide = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    # drop the old illustrated set (keep the app icon, which follows the phone's own icon style)
    for old in glob.glob(os.path.join(OUT, "*.png")):
        if os.path.basename(old) != "app_icon.png":
            os.remove(old)

    names = set(re.findall(r'"([a-z0-9]+(?:-[a-z0-9]+)*)"\s*(?:,\s*(?:cost:\s*\d+|\d+\s*,\s*"[^"]*"))?\);', open(DEFS, encoding="utf-8").read()))
    names |= set(re.findall(r'"([a-z0-9]+(?:-[a-z0-9]+)+|[a-z]+)"\);', open(DEFS, encoding="utf-8").read()))
    jobs = {n: n for n in names}
    jobs.update(EXTRA)

    missing = []
    for out_name, lucide_name in sorted(jobs.items()):
        src = os.path.join(lucide, lucide_name + ".svg")
        if not os.path.exists(src):
            missing.append(lucide_name)
            continue
        render(src, os.path.join(OUT, out_name + ".png"))
    print(f"rendered {len(jobs) - len(missing)} icons; missing: {missing}")


if __name__ == "__main__":
    main()
