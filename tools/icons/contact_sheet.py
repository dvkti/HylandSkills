"""Lays every generated icon on a dark grid so the set can be reviewed at a glance."""
import glob, os, sys
from PIL import Image, ImageDraw
src = os.path.join(os.path.dirname(__file__), "..", "..", "HylandSkills", "Assets", "Icons")
files = sorted(glob.glob(os.path.join(src, "*.png")))
cell, cols = 110, 12
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGBA", (cols * cell, rows * (cell + 16)), (37, 40, 47, 255))
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGBA").resize((88, 88), Image.LANCZOS)
    x, y = (i % cols) * cell + 11, (i // cols) * (cell + 16) + 6
    d.rounded_rectangle([x - 5, y - 5, x + 93, y + 93], 14, fill=(47, 51, 59, 255))
    sheet.alpha_composite(im, (x, y))
    d.text((x - 4, y + 94), os.path.basename(f)[:-4][:17], fill=(180, 180, 180, 255))
sheet.save(sys.argv[1])
print(sys.argv[1])
