#!/usr/bin/env python3
"""The Air-Brake Pocket-Book: Realistic Brakes' in-game booklet, in the manner of the Westinghouse books (Wood, 1909).

Pages are drawn as SVG at the game's booklet page size (1224 x 1584, its LicensePaper texture) and rendered to PNG
with rsvg-convert. The content is CHAPTERS below: a new brake gets a chapter of its own, and the contents page and
page numbers follow. A page whose blocks run past its foot stops the build.

    python3 -I make_booklet.py            writes pages/page-NN.svg and .png (previews), and the mod's
                                          src/RealisticBrakes/Booklet/: page-NN.jpg and booklet.json

booklet.json tells the mod how the pages are laid out: the thumb tabs' strip and each tab's pages (the game's
ChapterSelector), and the contents page's rows and the page each opens (page indices from 0, the cover).
"""
import json
import math
import subprocess
import textwrap
from pathlib import Path
from xml.sax.saxutils import escape

W, H = 1224, 1584
HERE = Path(__file__).resolve().parent
OUT = HERE / "pages"
SHIP = HERE.parent.parent / "src" / "RealisticBrakes" / "Booklet"

SERIF = "Liberation Serif"
GOTHIC = "DejaVu Sans Condensed"
INK = "#1e1a15"
PAPER = "#efe5cc"
RED = "#9d3427"
BLUE = "#2e4c80"
EDITION = "1.2.2"

LEFT, RIGHT = 110, W - 144

# The thumb tabs' strip down the page's free edge, px; the mod puts the game's chapter touchscreen over it.
TAB_X0, TAB_X1, TAB_Y0, TAB_Y1 = 1136, W, 190, 1450
# The contents page's rows: each a line, and a press on it opens that line's page.
ROW, ROW_Y0 = 74, 300
FOOT = H - 120          # blocks must end above this
BODY = 34               # body text size
WRAP = 58               # characters a full-width body line holds


# ---------------------------------------------------------------------------------------------------------------------
# Drawing


class Page:
    def __init__(self, background=PAPER):
        self.parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">', DEFS,
                      f'<rect width="{W}" height="{H}" fill="{background}"/>',
                      f'<rect width="{W}" height="{H}" filter="url(#grain)"/>',
                      f'<rect width="{W}" height="{H}" fill="url(#age)"/>']

    def add(self, s):
        self.parts.append(s)

    def text(self, x, y, s, size=BODY, family=SERIF, fill=INK, weight="normal", style="normal", anchor="start", spacing=0, extra=""):
        self.add(f'<text x="{x:.1f}" y="{y:.1f}" font-family="{family}" font-size="{size}" font-weight="{weight}" '
                 f'font-style="{style}" text-anchor="{anchor}" fill="{fill}" letter-spacing="{spacing}" {extra}>{escape(s)}</text>')

    def wrap(self, x, y, s, width, size=BODY, lead=1.28, **kw):
        for line in textwrap.wrap(s, width):
            self.text(x, y, line, size, **kw)
            y += size * lead
        return y

    def svg(self):
        return "\n".join(self.parts + ["</svg>"])


DEFS = f'''<defs>
<filter id="grain" x="0" y="0" width="100%" height="100%">
  <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="2" seed="7"/>
  <feColorMatrix type="matrix" values="0 0 0 0 0.35  0 0 0 0 0.28  0 0 0 0 0.16  0 0 0 0.09 0"/></filter>
<radialGradient id="age" cx="50%" cy="48%" r="75%">
  <stop offset="62%" stop-color="#000" stop-opacity="0"/><stop offset="100%" stop-color="#5a4320" stop-opacity="0.2"/></radialGradient>
<pattern id="hatch" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
  <line x1="0" y1="0" x2="0" y2="6" stroke="{INK}" stroke-width="1.4"/></pattern>
<pattern id="fine" width="5" height="5" patternUnits="userSpaceOnUse" patternTransform="rotate(-35)">
  <line x1="0" y1="0" x2="0" y2="5" stroke="{INK}" stroke-width="0.9"/></pattern>
<pattern id="cross" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
  <line x1="0" y1="0" x2="0" y2="6" stroke="{INK}" stroke-width="1.4"/><line x1="0" y1="0" x2="6" y2="0" stroke="{INK}" stroke-width="1.4"/></pattern>
</defs>'''


def polar(cx, cy, r, deg):
    a = math.radians(deg)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def label(p, x, y, s, anchor, size=25):
    p.text(x, y, s, size, GOTHIC, weight="bold", anchor=anchor, spacing=1.5)


def handle_bar(p, cx, cy, length, deg, width=15, knob=17):
    x, y = polar(cx, cy, length, deg)
    p.add(f'<line x1="{cx:.1f}" y1="{cy:.1f}" x2="{x:.1f}" y2="{y:.1f}" stroke="{INK}" stroke-width="{width}" stroke-linecap="round"/>')
    p.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{knob}" fill="url(#hatch)" stroke="{INK}" stroke-width="3"/>')


def valve_figure(p, cx, cy, R, names, angles, handle_at):
    """A brake valve from above with its handle's positions round it, every label upright and level."""
    p.add(f'<circle cx="{cx}" cy="{cy}" r="{R}" fill="{PAPER}" stroke="{INK}" stroke-width="5"/>')
    p.add(f'<clipPath id="vc{cy:.0f}"><circle cx="{cx}" cy="{cy}" r="{R-10}"/></clipPath>')
    p.add(f'<g clip-path="url(#vc{cy:.0f})"><circle cx="{cx}" cy="{cy}" r="{R-10}" fill="url(#fine)"/>'
          f'<circle cx="{cx-16}" cy="{cy-16}" r="{R-20}" fill="{PAPER}"/></g>')
    for k in range(6):
        bx, by = polar(cx, cy, R - 22, 30 + 60 * k)
        p.add(f'<circle cx="{bx:.1f}" cy="{by:.1f}" r="8" fill="url(#cross)" stroke="{INK}" stroke-width="1.6"/>')
    for i, (name, a) in enumerate(zip(names, angles), 1):
        x1, y1 = polar(cx, cy, R + 4, a)
        x2, y2 = polar(cx, cy, R + 34, a)
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="4"/>')
        c = math.cos(math.radians(a))
        lx, ly = polar(cx, cy, R + 46, a)
        if abs(c) < 0.3:
            label(p, lx, ly + 24, f"{i} {name}", "middle")
        else:
            label(p, lx + (8 if c > 0 else -8), ly + 9, f"{i} {name}", "start" if c > 0 else "end")
    handle_bar(p, cx, cy, R + 18, angles[handle_at])
    p.add(f'<circle cx="{cx}" cy="{cy}" r="26" fill="url(#cross)" stroke="{INK}" stroke-width="3"/>')


def self_lapping_figure(p, cx, cy, filling):
    """The diesels' self-lapping handle: a quadrant swept from the release end, its notches, and the handle in release."""
    R = 270
    a0, a1 = 208, 332
    p.add(f'<path d="M {cx} {cy} L {polar(cx,cy,R,a0)[0]:.1f} {polar(cx,cy,R,a0)[1]:.1f} A {R} {R} 0 0 1 '
          f'{polar(cx,cy,R,a1)[0]:.1f} {polar(cx,cy,R,a1)[1]:.1f} Z" fill="url(#fine)" stroke="{INK}" stroke-width="4"/>')
    p.add(f'<path d="M {cx} {cy} L {polar(cx,cy,R-40,a0+3)[0]:.1f} {polar(cx,cy,R-40,a0+3)[1]:.1f} A {R-40} {R-40} 0 0 1 '
          f'{polar(cx,cy,R-40,a1-3)[0]:.1f} {polar(cx,cy,R-40,a1-3)[1]:.1f} Z" fill="{PAPER}" stroke="{INK}" stroke-width="1.5"/>')
    marks = []
    if filling:
        marks.append(("FILLING STROKE*", 214))
    marks += [("RELEASE", 228), ("FIRST NOTCH", 240)]
    service = [251 + 10.2 * k for k in range(6)]
    marks += [("FULL SERVICE", 314), ("EMERGENCY", 326)]
    for a in service:
        x1, y1 = polar(cx, cy, R - 40, a)
        x2, y2 = polar(cx, cy, R, a)
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="2"/>')
    for name, a in marks:
        x1, y1 = polar(cx, cy, R - 40, a)
        x2, y2 = polar(cx, cy, R + 14, a)
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="5"/>')
        right = a > 270
        lx, ly = polar(cx, cy, R + 22, a)
        label(p, lx + (10 if right else -10), ly + 6, name, "start" if right else "end", 23)
    # The service notches under one bracket.
    b0, b1 = service[0] - 2, service[-1] + 2
    p.add(f'<path d="M {polar(cx,cy,R+12,b0)[0]:.1f} {polar(cx,cy,R+12,b0)[1]:.1f} A {R+12} {R+12} 0 0 1 '
          f'{polar(cx,cy,R+12,b1)[0]:.1f} {polar(cx,cy,R+12,b1)[1]:.1f}" fill="none" stroke="{INK}" stroke-width="2"/>')
    sx, sy = polar(cx, cy, R + 30, (b0 + b1) / 2)
    label(p, sx, sy, "SERVICE NOTCHES", "middle", 21)
    handle_bar(p, cx, cy, R - 20, 220, width=18, knob=20)
    p.add(f'<circle cx="{cx}" cy="{cy}" r="28" fill="url(#cross)" stroke="{INK}" stroke-width="3"/>')


def independent_figure(p, cx, cy, full):
    R = 150
    a0, a1 = 225, 315
    p.add(f'<path d="M {polar(cx,cy,R,a0)[0]:.1f} {polar(cx,cy,R,a0)[1]:.1f} A {R} {R} 0 0 1 {polar(cx,cy,R,a1)[0]:.1f} {polar(cx,cy,R,a1)[1]:.1f}" '
          f'fill="none" stroke="{INK}" stroke-width="5"/>')
    for k in range(7):
        a = a0 + (a1 - a0) * k / 6
        x1, y1 = polar(cx, cy, R, a)
        x2, y2 = polar(cx, cy, R + (22 if k in (0, 6) else 13), a)
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="{4 if k in (0, 6) else 2}"/>')
    lx, ly = polar(cx, cy, R + 34, a0)
    label(p, lx - 6, ly, "RELEASED", "end", 23)
    lx, ly = polar(cx, cy, R + 34, a1)
    label(p, lx + 6, ly, f"FULL, {full}", "start", 23)
    handle_bar(p, cx, cy, R - 12, a0 + 8, width=14, knob=16)
    p.add(f'<circle cx="{cx}" cy="{cy}" r="22" fill="url(#cross)" stroke="{INK}" stroke-width="3"/>')


def gauge(p, gx, gy, r, hands, legend):
    p.add(f'<circle cx="{gx}" cy="{gy}" r="{r+10}" fill="url(#hatch)" stroke="{INK}" stroke-width="3"/>')
    p.add(f'<circle cx="{gx}" cy="{gy}" r="{r}" fill="#f4ecd6" stroke="{INK}" stroke-width="2"/>')
    for k in range(17):
        v = k / 2
        a = 135 + 270 * v / 8
        major = k % 2 == 0
        x1, y1 = polar(gx, gy, r - 4, a)
        x2, y2 = polar(gx, gy, r - (17 if major else 10), a)
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="{2.2 if major else 1.2}"/>')
        if major:
            nx, ny = polar(gx, gy, r - 32, a)
            p.text(nx, ny + 7, str(int(v)), 19, anchor="middle")
    p.text(gx, gy + 34, "BAR", 14, GOTHIC, weight="bold", anchor="middle", spacing=2)
    for v, col, length in hands:
        a = 135 + 270 * v / 8
        tx, ty = polar(gx, gy, length, a)
        b1x, b1y = polar(gx, gy, 8, a + 90)
        b2x, b2y = polar(gx, gy, 8, a - 90)
        ex, ey = polar(gx, gy, 20, a + 180)
        p.add(f'<polygon points="{tx:.1f},{ty:.1f} {b1x:.1f},{b1y:.1f} {ex:.1f},{ey:.1f} {b2x:.1f},{b2y:.1f}" fill="{col}" stroke="{INK}" stroke-width="1.2"/>')
    p.add(f'<circle cx="{gx}" cy="{gy}" r="7" fill="{INK}"/>')
    spans = "".join(f'<tspan fill="{col}">{escape(s)}</tspan>' for s, col in legend)
    p.add(f'<text x="{gx}" y="{gy + r + 42}" font-family="{GOTHIC}" font-size="22" font-weight="bold" text-anchor="middle" letter-spacing="1" xml:space="preserve">{spans}</text>')


def gauges_figure(p, cx, cy):
    gauge(p, cx - 150, cy, 100, [(6.4, RED, 82), (5.0, BLUE, 70)], [("MR", RED), (" / ", INK), ("ER", BLUE)])
    gauge(p, cx + 150, cy, 100, [(5.0, INK, 82), (0.0, RED, 70)], [("BP", INK), (" / ", INK), ("BC", RED)])


def air_figure(p, cx, cy):
    """The train's air: the engine's main reservoir and brake valve, the pipe, and each car's valve, reservoir, and cylinder."""
    y = cy
    # The engine.
    ex = LEFT
    p.add(f'<rect x="{ex}" y="{y-110}" width="210" height="150" fill="{PAPER}" stroke="{INK}" stroke-width="4"/>')
    p.add(f'<rect x="{ex+20}" y="{y-10}" width="170" height="36" rx="18" fill="url(#hatch)" stroke="{INK}" stroke-width="3"/>')
    p.add(f'<rect x="{ex+130}" y="{y-92}" width="50" height="44" fill="url(#cross)" stroke="{INK}" stroke-width="3"/>')
    label(p, ex + 105, y - 120, "ENGINE", "middle", 21)
    label(p, ex + 105, y + 70, "MAIN RESERVOIR", "middle", 18)
    label(p, ex + 120, y - 62, "BRAKE VALVE", "end", 15)
    # The pipe, from the brake valve along the train.
    py = y + 80
    p.add(f'<polyline points="{ex+180},{y-70} {ex+240},{y-70} {ex+240},{py} {RIGHT-10},{py}" fill="none" stroke="{INK}" stroke-width="6"/>')
    label(p, RIGHT - 10, py + 34, "BRAKE PIPE", "end", 20)
    for k in range(3):
        x = ex + 300 + k * 230
        p.add(f'<rect x="{x}" y="{y-90}" width="190" height="120" fill="{PAPER}" stroke="{INK}" stroke-width="3"/>')
        p.add(f'<rect x="{x+16}" y="{y-70}" width="100" height="34" rx="17" fill="url(#hatch)" stroke="{INK}" stroke-width="2.5"/>')
        p.add(f'<rect x="{x+130}" y="{y-74}" width="40" height="40" fill="url(#cross)" stroke="{INK}" stroke-width="2.5"/>')
        p.add(f'<rect x="{x+60}" y="{y-14}" width="70" height="30" fill="{PAPER}" stroke="{INK}" stroke-width="2.5"/>')
        p.add(f'<line x1="{x+150}" y1="{y-34}" x2="{x+150}" y2="{py}" stroke="{INK}" stroke-width="3"/>')
        p.add(f'<line x1="{x+150}" y1="{y-6}" x2="{x+130}" y2="{y-6}" stroke="{INK}" stroke-width="2.5"/>')
        p.add(f'<line x1="{x+116}" y1="{y-53}" x2="{x+130}" y2="{y-53}" stroke="{INK}" stroke-width="2.5"/>')
        if k == 0:
            label(p, x + 66, y - 100, "RESERVOIR", "middle", 17)
            label(p, x + 150, y - 100, "VALVE", "start", 17)
            label(p, x + 95, y + 46, "CYLINDER", "middle", 17)


def lever(p, cx, cy, letters, at, title):
    R = 62
    span = 90 if len(letters) == 2 else 135
    a0 = 270 - span / 2
    p.add(f'<path d="M {polar(cx,cy,R,a0)[0]:.1f} {polar(cx,cy,R,a0)[1]:.1f} A {R} {R} 0 0 1 {polar(cx,cy,R,a0+span)[0]:.1f} {polar(cx,cy,R,a0+span)[1]:.1f}" fill="none" stroke="{INK}" stroke-width="3"/>')
    for i, s in enumerate(letters):
        a = a0 + span * i / (len(letters) - 1)
        lx, ly = polar(cx, cy, R + 34, a)
        label(p, lx, ly + 8, s, "middle", 22)
    handle_bar(p, cx, cy, R - 16, a0 + span * at / (len(letters) - 1), width=10, knob=11)
    p.add(f'<circle cx="{cx}" cy="{cy}" r="14" fill="url(#cross)" stroke="{INK}" stroke-width="2.5"/>')
    p.text(cx, cy + 56, title, 22, GOTHIC, weight="bold", anchor="middle", spacing=1.5)


def levers_figure(p, cx, cy):
    p.text(cx - 260, cy - 120, "European car", 26, style="italic", anchor="middle")
    p.text(cx + 260, cy - 120, "American car", 26, style="italic", anchor="middle")
    lever(p, cx - 360, cy, ["G", "P"], 0, "MODE")
    lever(p, cx - 160, cy, ["IN", "OUT"], 0, "ISOLATE")
    lever(p, cx + 160, cy, ["EX", "LP", "HP", "SD"], 0, "RETAINER")
    lever(p, cx + 380, cy, ["IN", "OUT"], 0, "CUT-OUT")
    p.add(f'<line x1="{cx}" y1="{cy-140}" x2="{cx}" y2="{cy+70}" stroke="{INK}" stroke-width="1.5" stroke-dasharray="6 6"/>')


# ---------------------------------------------------------------------------------------------------------------------
# Content: chapters of blocks. A block is (kind, ...):
#   ("h", text)            a chapter's heading        ("h2", text)        a sub-heading
#   ("p", text)            a paragraph                ("note", text)      an italic paragraph
#   ("pos", [(name, text)])  a handle's positions, numbered
#   ("fig", draw, height, caption)  a figure, drawn by draw(page, cx, cy) about its middle
#   ("page",)              starts the chapter's next page

H6 = ["RELEASE", "RUNNING", "HOLDING", "LAP", "SERVICE", "EMERGENCY"]
MANUAL = ["RELEASE", "RUNNING", "LAP", "SERVICE", "EMERGENCY"]
S6 = ["RELEASE", "RUNNING", "LAP", "SLOW APP.", "QUICK APP."]

CHAPTERS = [
    dict(number="1", title="The Air Brake", head="The Air Brake.", tab="THE AIR BRAKE", blocks=[
        ("h", "1. How the Air Brake Works."),
        ("p", "Every car carries its own length of brake pipe, its own reservoirs, and its own valve. The pipe runs the length of the train and is kept charged from the engine's main reservoir, at 5 bar (about 70 lb)."),
        ("fig", air_figure, 250, "FIG. 1.—The train's air: pipe, reservoirs, valves, and cylinders."),
        ("p", "To apply the brakes the engineman lowers the pipe: each car's valve feels the fall and lets its reservoir's air into its brake cylinder. A deeper reduction brakes harder; 1.5 bar is full service. Raising the pipe again releases the brakes."),
        ("p", "The change runs down the train at about the speed of sound, so the rear brakes later than the front. A freight car's cylinder takes its real 18 to 30 seconds to fill, and a long train takes minutes to release and recharge."),
        ("p", "A leak or a slow drift does not apply the brakes; a reduction does. A sudden fall, as from a parted train, is an emergency."),
        ("note", "The equipment setting chooses European (UIC), the default, or American (AB); each has its own chapters."),
    ]),
    dict(number="2", title="European Equipment", head="European Equipment.", tab="EUROPEAN", group=True, blocks=[]),
    dict(number="2.1", title="Diesels: the self-lapping brake valve", head="European Equipment. The Diesels.", blocks=[
        ("h", "2.1. The Self-Lapping Brake Valve."),
        ("p", "On the DE2, DE6, DH4, DM1U, and BE2 each notch of the handle asks for a reduction, and the valve holds the pipe there by itself."),
        ("fig", lambda p, cx, cy: self_lapping_figure(p, cx, cy + 143, True), 345, "FIG. 2.—The self-lapping handle, in Release. *European equipment only."),
        ("pos", [
            ("Filling stroke", "Past release. Held 0.4 s, it charges a long train quickly, and springs back when let go. Held long, it overcharges the train."),
            ("Release", "Running: the pipe is kept at 5 bar and the brakes come off."),
            ("First notch", "The least reduction."),
            ("Service", "Each notch a deeper reduction, up to full service at 1.5 bar."),
            ("Emergency", "Past full service. The pipe is opened wide; the handle stays until moved back."),
        ]),
        ("note", "European cars release in steps: a notch or two back, and the brakes come off a little."),
    ]),
    dict(number="2.2", title="Steam engines and DM3: the brake valve", head="European Equipment. The Steam Engines.", blocks=[
        ("h", "2.2. The Automatic Brake Valve."),
        ("p", "On the S282, S060, DM3, and the Custom Car Loader steamers the handle is manual-lap: it does not stop a reduction by itself. Hold it in service until the blue hand shows the reduction wanted, then lap it."),
        ("fig", lambda p, cx, cy: valve_figure(p, cx, cy - 20, 120, MANUAL, [200, 150, 102, 58, 18], 1), 395, "FIG. 3.—The brake valve from above; handle in Running position."),
        ("pos", [
            ("Release", "Main reservoir air straight into the pipe, to charge a long train quickly. It stays here; left too long, the train overcharges."),
            ("Running", "The pipe is kept at 5 bar; the brakes release."),
            ("Lap", "Holds the reduction made."),
            ("Service", "The equalizing reservoir comes down while the handle is held here."),
            ("Emergency", "The pipe is opened wide."),
        ]),
        ("note", "The H-6 and S-6 (3.2) come only with American equipment."),
    ]),
    dict(number="2.3", title="Cars: the UIC distributor, G and P", head="European Equipment. The Cars.", blocks=[
        ("h", "2.3. The UIC Distributor."),
        ("p", "European cars release in steps: let the pipe up a little and the brakes come off a little. The reservoirs top up whenever the pipe stands above them, so repeated applications do not use the brake up."),
        ("h2", "G and P."),
        ("p", "Each freight car's mode lever sets G, goods, or P, passenger. In G the cylinder fills in 18 to 30 seconds and releases in 45 to 60; in P, 3 to 5 and 15 to 20. Coaches and engines run in P."),
        ("h2", "Empty and Loaded."),
        ("p", "Every freight car brakes with its own force, not its load's. A full car stops as it always did and a lighter one stops shorter. Each car's empty/loaded valve is set from its load, so an empty car brakes gently enough not to slide its wheels."),
        ("h2", "The Isolating Cock."),
        ("p", "Closed, the car's brake is cut out and the car runs unbraked."),
    ]),
    dict(number="3", title="American Equipment", head="American Equipment.", tab="AMERICAN", group=True, blocks=[]),
    dict(number="3.1", title="Diesels: the 26-L brake valve", head="American Equipment. The Diesels.", blocks=[
        ("h", "3.1. The 26-L Brake Valve."),
        ("p", "The diesels' handle is self-lapping, as on European equipment, but the 26-L has no filling stroke."),
        ("fig", lambda p, cx, cy: self_lapping_figure(p, cx, cy + 143, False), 345, "FIG. 4.—The 26-L handle, in Release."),
        ("pos", [
            ("Release", "Running: the pipe is kept at 5 bar."),
            ("First notch", "The least reduction, 0.45 bar, enough that a long train's brakes stay on."),
            ("Service", "Each notch a deeper reduction, up to full service at 1.5 bar."),
            ("Emergency", "Past full service; it stays until moved back."),
        ]),
        ("note", "American cars have no graduated release: any release releases in full. Let the train recharge before you apply again."),
    ]),
    dict(number="3.2", title="Steam engines and DM3: the E-T brake valve", head="American Equipment. The E-T Brake.", blocks=[
        ("h", "3.2. The H-6 Automatic Brake Valve."),
        ("p", "Only with American equipment do the steam engines and the DM3 have the E-T: the H-6 and the S-6."),
        ("fig", lambda p, cx, cy: valve_figure(p, cx, cy - 20, 115, H6, [200, 160, 124, 90, 54, 18], 1), 395, "FIG. 5.—The H-6 brake valve from above; handle in Running position."),
        ("pos", [
            ("Release", "Main reservoir air straight into the pipe, to release a long train. It hisses as a warning: come back within a few seconds."),
            ("Running", "The pipe is kept at 5 bar. Only here do the engine's brakes come off, all at once."),
            ("Holding", "The train releases and recharges; the engine's brakes stay on."),
            ("Lap", "Holds the reduction. Leaks are not made up."),
            ("Service", "The blue hand falls about 0.35 bar every 2 seconds."),
            ("Emergency", "The pipe opens wide; the engine's cylinders reach 4.7 bar."),
        ]),
        ("page",),
        ("h2", "To Release a Long Train."),
        ("p", "Release for a few seconds; then Holding while the train releases and recharges, the engine's brakes holding the slack; then Running."),
        ("h2", "The Equalizing Reservoir."),
        ("p", "In Release it charges from the main reservoir through a small port, so the blue hand lags the pipe. In Running and Holding it follows the pipe, and the valve feeds the pipe only up to 5 bar. Lapped straight from Release, the valve lets the pipe at the engine down to the blue hand: give it a few seconds in Running or Holding first."),
        ("h2", "The Air Pump."),
        ("p", "The pump's governor follows the handle: it keeps the main reservoir about 1.4 bar over the pipe in running and holding, and lets it rise to 7.6 bar in lap and service."),
        ("page",),
        ("h2", "The S-6 Independent Brake Valve."),
        ("p", "It is not self-lapping: like the H-6, it applies until it is lapped, from a reducing valve set at 3.1 bar (45 lb)."),
        ("fig", lambda p, cx, cy: valve_figure(p, cx, cy - 30, 85, S6, [196, 150, 106, 62, 18], 1), 310, "FIG. 6.—The S-6 independent brake valve; handle in Running position."),
        ("pos", [
            ("Release", "Below Running. Hold the handle here and the engine's brakes let off; what is left stays on. It springs back to Running. A handle without notches: click and hold it."),
            ("Running", "The engine's brakes let go here, but only with the H-6 in Running too."),
            ("Lap", "Holds the engine's brakes, even through the train's release and recharge."),
            ("Slow App.", "Applies gradually: lap it at the pressure you want."),
            ("Quick App.", "Applies at once. It springs back to Slow Application."),
        ]),
        ("p", "After an emergency from the engine's own handle, the brakes come back as soon as you let go of Release, as on the real valve."),
    ]),
    dict(number="3.3", title="Cars: the AB valve, retainers, load braking", head="American Equipment. The Cars.", blocks=[
        ("h", "3.3. The AB Valve."),
        ("p", "Any release releases in full. The reservoirs recharge only once the car is released, so a car applied again too soon brakes weaker, and one applied over and over runs out of air."),
        ("h2", "Emergency."),
        ("p", "An emergency adds each car's emergency reservoir, about a fifth more braking, and vents the pipe at every car, so it runs down the train fast. The pipe cannot be recharged for about a minute."),
        ("h2", "Release along the Train."),
        ("p", "Cars near the engine release and recharge more slowly, so a long train lets go together, and the head end cannot rob the rear of air."),
        ("h2", "Retainers."),
        ("p", "The retaining valve holds cylinder air through a release, to hold a train on a long grade while it recharges: EX, normal exhaust; LP, 0.7 bar (10 lb); HP, 1.4 bar (20 lb); SD, slow exhaust. A held cylinder blows down from 50 to 15 lb in 20 to 25 seconds."),
        ("page",),
        ("h2", "Load Braking."),
        ("p", "By default American cars have no empty/load valve, as in the steam era: every car brakes with the force sized for it empty, so a loaded train takes much longer to stop. Use the retainers on long grades. A setting gives the cars automatic empty/load instead."),
        ("h2", "Coaches."),
        ("p", "Coaches have graduated-release passenger equipment: they release in steps, and an emergency gives them about a sixth more than full service."),
        ("h2", "The Cut-Out Cock."),
        ("p", "Closed, the car's brake is cut out and the car runs unbraked."),
    ]),
    dict(number="4", title="At the car: levers, bleed handle, hand brakes", head="At the Car.", tab="AT THE CAR", blocks=[
        ("h", "4. At the Car."),
        ("p", "Beside each car's bleed handle, a row of levers sets the car's brake, each lever's positions printed round it."),
        ("fig", levers_figure, 270, "FIG. 7.—The lever rows of a European and an American car."),
        ("h2", "The Bleed Handle."),
        ("p", "Click it for a short pull: the car lets go of its brake and keeps its reservoirs' air. A European car lets go within seconds, then empties its cylinder at its G or P time; an American car empties its cylinder in about 20 seconds and stays released until its pipe is recharged."),
        ("p", "Hold it past 3 seconds to drain the car's reservoirs as well, about 50 seconds to empty: this takes a car off air before you set it out."),
        ("h2", "Hand Brakes."),
        ("p", "A hand brake gives half a full application and holds only what it can. A loaded car's holds it alone on a steep grade; a cut on a 1 % grade needs about one car in three."),
    ]),
    dict(number="5", title="The independent, bailing off, the gauges", head="The Engine's Own Brakes.", tab="THE ENGINE", blocks=[
        ("h", "5. The Independent, Bailing Off, and the Gauges."),
        ("p", "The independent applies the engine's own brakes only: up to 3.8 bar, on in about 3 seconds and off in about 4. On European engines and the diesels the engine's train brake lets off at a passenger brake's pace, about 18 seconds. The E-T's independent is not self-lapping (chapter 3.2)."),
        ("h2", "Bailing Off."),
        ("p", "With the train brake applied, click the independent lever: the engine's brakes let go and the train's stay on. Engines joined by MU cable bail off together. A further reduction applies the engine again; a release clears the bail. On the E-T, click and hold (chapter 3.2)."),
        ("fig", gauges_figure, 300, "FIG. 8.—The gauges in running: main reservoir red, equalizing reservoir blue; brake pipe black, cylinder red."),
        ("p", "Look at the train brake, the independent, or the brake valve cut-out, and the text by the crosshair names its position."),
    ]),
    dict(number="6", title="Settings", head="Settings.", tab="SETTINGS", blocks=[
        ("h", "6. Settings."),
        ("p", "The settings are in Unity Mod Manager's window, Ctrl+F10."),
        ("pos", [
            ("Realistic brake", "Off gives the game's own brake back at once, from the pressures this one left."),
            ("Equipment", "European or American. American gives the steam engines and DM3 the E-T, the H-6 and S-6. Changing it re-reads every car."),
            ("Load braking", "American cars: Period, no empty/load valve, or EmptyLoad, automatic."),
            ("Force from car", "Loaded and empty cars stop differently."),
            ("G/P lever", "The freight cars' mode as they come into service."),
            ("Added notches", "The filling stroke, emergency, release, and holding on the handles."),
            ("ER hand", "The equalizing reservoir's blue hand on the steam engines' gauge."),
            ("Bail off", "Clicking the independent bails off."),
            ("Controls' text", "The text by the crosshair on the brake controls."),
            ("Overlay", "Each car's brake pressures, for the train you are on."),
        ]),
    ]),
]


# ---------------------------------------------------------------------------------------------------------------------
# Layout


def frame(p, head, number):
    p.text(W / 2, 112, head, 38, weight="bold", anchor="middle")
    p.text(W / 2, H - 56, f"[ {number} ]", 34, anchor="middle")


def lay_out(chapter, first_page):
    """Lays a chapter's blocks out on as many pages as it says; returns its pages."""
    pages = []
    p, y, n = None, 0, first_page

    def new_page():
        nonlocal p, y, n
        p = Page()
        frame(p, chapter["head"], n)
        pages.append(p)
        n += 1
        y = 200

    new_page()
    for block in chapter["blocks"]:
        kind = block[0]
        if kind == "page":
            new_page()
            continue
        if kind == "h":
            p.text(LEFT, y, block[1], 44, weight="bold")
            y += 70
        elif kind == "h2":
            y += 8
            p.text(LEFT, y, block[1], 36, weight="bold", style="italic")
            y += 50
        elif kind in ("p", "note"):
            y = p.wrap(LEFT, y, block[1], WRAP, style="italic" if kind == "note" else "normal") + 16
        elif kind == "pos":
            for i, (name, what) in enumerate(block[1], 1):
                p.text(LEFT, y, f"{i}. {name}" + ("" if name.endswith(".") else "."), BODY, weight="bold")
                y = p.wrap(LEFT + 285, y, what, 42, lead=1.2) + 8
            y += 10
        elif kind == "fig":
            _, draw, height, caption = block
            draw(p, W / 2, y + height / 2 - 20)
            y += height
            y = p.wrap(W / 2, y, caption, 66, 28, anchor="middle") + 22
        if y > FOOT:
            OVERFLOWS.append(f"chapter {chapter['number']} runs {y - FOOT:.0f} px past the foot of page {n - 1}")
    return pages


OVERFLOWS = []


def cover():
    p = Page("#3d4a3a")
    gold = "#d8c189"
    p.add(f'<rect x="60" y="60" width="{W-120}" height="{H-120}" fill="none" stroke="{gold}" stroke-width="6"/>')
    p.add(f'<rect x="80" y="80" width="{W-160}" height="{H-160}" fill="none" stroke="{gold}" stroke-width="2"/>')
    p.text(W / 2, 360, "THE", 54, fill=gold, weight="bold", anchor="middle", spacing=10)
    p.text(W / 2, 480, "AIR-BRAKE", 120, fill=gold, weight="bold", anchor="middle", spacing=6)
    p.text(W / 2, 600, "POCKET-BOOK", 96, fill=gold, weight="bold", anchor="middle", spacing=6)
    p.add(f'<line x1="300" y1="660" x2="{W-300}" y2="660" stroke="{gold}" stroke-width="3"/>')
    p.text(W / 2, 740, "for Enginemen of the Valley", 48, fill=gold, style="italic", anchor="middle")
    p.text(W / 2, 830, "European and American Equipment", 40, fill=gold, anchor="middle")
    cx, cy = W / 2, 1110
    p.add(f'<circle cx="{cx}" cy="{cy}" r="150" fill="none" stroke="{gold}" stroke-width="8"/>')
    p.add(f'<circle cx="{cx}" cy="{cy}" r="120" fill="none" stroke="{gold}" stroke-width="3"/>')
    for k in range(12):
        x1, y1 = polar(cx, cy, 40, 30 * k)
        x2, y2 = polar(cx, cy, 118, 30 * k)
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{gold}" stroke-width="6"/>')
    p.add(f'<circle cx="{cx}" cy="{cy}" r="40" fill="{gold}"/>')
    p.text(W / 2, 1400, f"REALISTIC BRAKES  ·  EDITION {EDITION}", 34, fill=gold, anchor="middle", spacing=4)
    return p


def contents(entries):
    """The contents page, a line to a row; returns it and each row's page (a group's heading opens its first chapter)."""
    p = Page()
    frame(p, "The Air-Brake Pocket-Book.", 2)
    p.text(W / 2, 200, "CONTENTS.", 52, weight="bold", anchor="middle", spacing=4)
    p.add(f'<line x1="420" y1="228" x2="{W-420}" y2="228" stroke="{INK}" stroke-width="2"/>')
    targets = []
    for i, (number, title, page, group) in enumerate(entries):
        y = ROW_Y0 + i * ROW + 50
        if group:
            p.text((LEFT + RIGHT) / 2, y, f"{number}. {title.upper()}", 34, weight="bold", anchor="middle", spacing=2)
            targets.append(next(e[2] for e in entries[i + 1:] if e[2] is not None) - 1)
            continue
        sub = "." in number
        x = LEFT + (44 if sub else 0)
        p.text(x, y, f"{number}.", 33, weight="normal" if sub else "bold")
        p.text(x + 74, y, title, 33, style="italic" if sub else "normal")
        p.add(f'<line x1="{x + 88 + len(title) * 14.8:.0f}" y1="{y-8}" x2="{RIGHT-56}" y2="{y-8}" stroke="{INK}" stroke-width="3" stroke-dasharray="2 12"/>')
        p.text(RIGHT, y, str(page), 33, anchor="end")
        targets.append(page - 1)
    last = ROW_Y0 + len(entries) * ROW
    p.wrap(LEFT, max(last + 70, FOOT - 100), "Press a line, or a tab at the page's edge, to turn to it. New schedules of brakes are described in chapters of their own.", 62, 29, style="italic")
    return p, targets


def tabs(p, items, active):
    """The thumb tabs down the free edge, the page's own inverted."""
    th = (TAB_Y1 - TAB_Y0) / len(items)
    for i, item in enumerate(items):
        y = TAB_Y0 + i * th
        on = i == active
        p.add(f'<rect x="{TAB_X0}" y="{y:.1f}" width="{TAB_X1 - TAB_X0}" height="{th:.1f}" fill="{INK if on else "#e2d4b0"}" stroke="{INK}" stroke-width="2.5"/>')
        cx, cy = (TAB_X0 + TAB_X1) / 2, y + th / 2
        p.add(f'<g transform="translate({cx + 7:.1f},{cy:.1f}) rotate(90)"><text x="0" y="0" font-family="{GOTHIC}" font-size="20" '
              f'font-weight="bold" text-anchor="middle" fill="{PAPER if on else INK}" letter-spacing="1.5">{escape(item["label"])}</text></g>')


def build():
    pages, entries, owner, n = [], [], [], 3
    # Each tab's pages, from 0 (the cover): the contents, then each top-level chapter and its sections.
    items = [dict(label="CONTENTS", first=1, last=1)]
    for chapter in CHAPTERS:
        top = chapter["number"].split(".")[0]
        if "tab" in chapter:
            items.append(dict(label=chapter["tab"], key=top, first=None, last=None))
        if chapter.get("group"):
            entries.append((chapter["number"], chapter["title"], None, True))
            continue
        laid = lay_out(chapter, n)
        entries.append((chapter["number"], chapter["title"], n, False))
        item = next(i for i in items if i.get("key") == top)
        item["first"] = n - 1 if item["first"] is None else item["first"]
        item["last"] = n - 1 + len(laid) - 1
        pages += laid
        owner += [items.index(item)] * len(laid)
        n += len(laid)
    if OVERFLOWS:
        raise SystemExit("\n".join(OVERFLOWS))
    contents_page, targets = contents(entries)
    pages = [cover(), contents_page] + pages
    owner = [None, 0] + owner
    for page, active in zip(pages[1:], owner[1:]):
        tabs(page, items, active)
    # The game's ChapterSelector wants the chapters to run on, the cover first and the last tab to the last page.
    spans = [(0, 0)] + [(i["first"], i["last"]) for i in items]
    if any(b[0] != a[1] + 1 for a, b in zip(spans, spans[1:])) or spans[-1][1] != len(pages) - 1:
        raise SystemExit(f"The tabs' pages don't run on: {spans}")
    if not all(0 < t < len(pages) for t in targets):
        raise SystemExit(f"A contents line opens no page: {targets}")

    OUT.mkdir(exist_ok=True)
    SHIP.mkdir(parents=True, exist_ok=True)
    for old in list(OUT.glob("page-*")) + list(SHIP.glob("page-*")):
        old.unlink()
    files = []
    for i, page in enumerate(pages, 1):
        svg = OUT / f"page-{i:02d}.svg"
        svg.write_text(page.svg())
        png = svg.with_suffix(".png")
        subprocess.run(["rsvg-convert", "-w", str(W), "-h", str(H), str(svg), "-o", str(png)], check=True)
        jpg = SHIP / f"page-{i:02d}.jpg"
        subprocess.run(["magick", str(png), "-strip", "-quality", "90", "-sampling-factor", "4:4:4", str(jpg)], check=True)
        files.append(jpg.name)
    manifest = {
        "edition": EDITION,
        "size": [W, H],
        "pages": files,
        "tabs": {"x0": TAB_X0, "x1": TAB_X1, "y0": TAB_Y0, "y1": TAB_Y1,
                 "items": [{"label": i["label"], "first": i["first"], "last": i["last"]} for i in items]},
        "contents": {"page": 1, "x0": LEFT - 20, "x1": RIGHT + 20, "y0": ROW_Y0, "rowHeight": ROW, "targets": targets},
    }
    (SHIP / "booklet.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"{len(pages)} pages in {OUT}; shipped to {SHIP}")


if __name__ == "__main__":
    build()
