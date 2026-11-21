#!/usr/bin/env python3
"""The Air-Brake Pocket-Book as the mod ships it: make_booklet.py's chapters with the vacuum brake's, chapter 7.

The vacuum brake was held out of public releases until 1.5.0, so its chapter was kept here, apart from the rest. Run
this to remake the booklet: its pages are the committed ones (src/RealisticBrakes/Booklet), and scripts/package.sh -test
and scripts/deploy.sh make them afresh the same way.

    python3 -I vacuum_chapter.py                    remakes the committed pages, src/RealisticBrakes/Booklet, and the
                                                    previews in art/booklet/pages
    python3 -I vacuum_chapter.py DEST [PREVIEWS]    writes page-NN.jpg and booklet.json to DEST, and the pages' SVG and
                                                    PNG to PREVIEWS (by default a folder that is thrown away)
"""
import importlib.util
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("make_booklet", HERE / "make_booklet.py")
mb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mb)

INK, RED, polar, label = mb.INK, mb.RED, mb.polar, mb.label


def dial(p, gx, gy, r, top, step, every, unit, mark, hands, legend):
    """A round gauge from 0 to top over the stock dials' 270 degrees, a red mark at mark if any."""
    p.add(f'<circle cx="{gx}" cy="{gy}" r="{r+10}" fill="url(#hatch)" stroke="{INK}" stroke-width="3"/>')
    p.add(f'<circle cx="{gx}" cy="{gy}" r="{r}" fill="#f4ecd6" stroke="{INK}" stroke-width="2"/>')
    at = lambda v: 135 + 270 * v / top
    for k in range(int(top / step) + 1):
        v = k * step
        major = k % every == 0
        x1, y1 = polar(gx, gy, r - 4, at(v))
        x2, y2 = polar(gx, gy, r - (17 if major else 10), at(v))
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="{2.2 if major else 1.2}"/>')
        if major:
            nx, ny = polar(gx, gy, r - 32, at(v))
            p.text(nx, ny + 7, str(int(v)), 18, anchor="middle")
    if mark is not None:
        x1, y1 = polar(gx, gy, r - 2, at(mark))
        x2, y2 = polar(gx, gy, r - 22, at(mark))
        p.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{RED}" stroke-width="5"/>')
    p.text(gx, gy + 40, unit, 14, mb.GOTHIC, weight="bold", anchor="middle", spacing=2)
    for v, col, length in hands:
        a = at(v)
        tx, ty = polar(gx, gy, length, a)
        b1x, b1y = polar(gx, gy, 8, a + 90)
        b2x, b2y = polar(gx, gy, 8, a - 90)
        ex, ey = polar(gx, gy, 20, a + 180)
        p.add(f'<polygon points="{tx:.1f},{ty:.1f} {b1x:.1f},{b1y:.1f} {ex:.1f},{ey:.1f} {b2x:.1f},{b2y:.1f}" fill="{col}" stroke="{INK}" stroke-width="1.2"/>')
    p.add(f'<circle cx="{gx}" cy="{gy}" r="7" fill="{INK}"/>')
    p.text(gx, gy + r + 42, legend, 22, mb.GOTHIC, weight="bold", anchor="middle", spacing=1)


def gauges_figure(p, cx, cy):
    dial(p, cx - 150, cy, 100, 30, 1, 5, "IN. VACUUM", 21, [(21, RED, 70), (21, INK, 82)], "TRAIN PIPE / CHAMBER")
    dial(p, cx + 150, cy, 100, 200, 10, 5, "LB. STEAM", None, [(0, INK, 82)], "STEAM BRAKE")


STEAM_VALVE = ["RELEASE", "RUNNING", "LAP", "APPLY", "EMERGENCY"]

CHAPTERS = [
    dict(number="7", title="Vacuum Equipment", head="Vacuum Equipment.", tab="VACUUM", group=True, blocks=[]),
    dict(number="7.1", title="How the vacuum brake works", head="Vacuum Equipment. The Brake.", blocks=[
        ("h", "7.1. How the Vacuum Brake Works."),
        ("p", "On this setting every engine and car has the automatic vacuum brake, at 21 inches. The engine keeps the air out of the train pipe, and each car's chamber holds that vacuum above its piston."),
        ("p", "To apply the brakes, let air into the pipe. Air under the piston lifts it against the vacuum over it: the less vacuum left in the pipe, the harder the brake. Making vacuum again releases it, by degrees if you wish."),
        ("fig", gauges_figure, 300, "FIG. 9.—The gauges of a steam engine in running: the pipe's black hand, the chamber's red; the steam brake."),
        ("p", "The chamber is sealed, so repeated applications do not use the brake up. A parted train applies both parts."),
        ("p", "Direct admission valves, on by default, let air straight under each piston, so a long train brakes in about half a minute."),
        ("note", "The game's end cocks stand for the hoses: closed is a hose on its dummy; open and uncoupled is a hose off it, which applies the train."),
    ]),
    dict(number="7.2", title="Steam engines: the ejectors", head="Vacuum Equipment. The Steam Engines.", blocks=[
        ("h", "7.2. The Ejectors and the Brake Valve."),
        ("p", "The air pump's handwheel is the small ejector's steam valve; on the S282 and S060 it is outside the cab."),
        ("fig", lambda p, cx, cy: mb.valve_figure(p, cx, cy - 20, 120, STEAM_VALVE, [200, 150, 102, 58, 18], 1), 395, "FIG. 10.—The vacuum brake valve; handle in Running position."),
        ("pos", [
            ("Release", "The large ejector, on its own steam even with the wheel shut: to make vacuum and release quickly."),
            ("Running", "The small ejector holds the pipe, if its wheel is open. Shut, nothing keeps the vacuum up."),
            ("Lap", "The pipe is cut off. It holds, and leaks creep the brake on."),
            ("Apply", "Air enters while the handle stands here. About 3 seconds takes ten cars to about 10 in."),
            ("Emergency", "The full port: every car is on within seconds."),
        ]),
        ("page",),
        ("h2", "Steam."),
        ("p", "The small ejector takes about 300 lb of steam an hour with its wheel open, the large about 750 more in Release. Both weaken as the boiler falls, and stop at about 1 bar."),
        ("h2", "Making Vacuum."),
        ("p", "A cold train of ten boxcars reaches 18 in at the rear in about 80 seconds on the small ejector. With the cars' chambers already exhausted, the large ejector restores the pipe in about 25. Open the wheel before you leave Release, or the vacuum will not stay up in Running."),
        ("h2", "The Steam Brake."),
        ("p", "The engine and tender brake by steam. The brake follows the pipe both ways: 180 lb with no vacuum, nothing by about 20.5 in. The independent applies it by hand, and the main reservoir gauge shows it."),
        ("p", "It cannot be bailed off. It comes off only as the vacuum comes back."),
    ]),
    dict(number="7.3", title="Diesels: the exhausters", head="Vacuum Equipment. The Diesels.", blocks=[
        ("h", "7.3. The Exhausters and the Handle."),
        ("p", "A diesel's exhausters run while its engine does. The self-lapping handle sets the vacuum and the valve holds it."),
        ("fig", lambda p, cx, cy: mb.self_lapping_figure(p, cx, cy + 143, True), 345, "FIG. 11.—The handle in Release, which here holds 21 in. *Held; it springs back."),
        ("pos", [
            ("Filling stroke", "Past release, held: the exhausters at full speed, to make vacuum quickly. It springs back."),
            ("Release", "Running: the pipe is held at 21 in and the brakes come off."),
            ("Service", "The handle's travel sets the vacuum, from 21 in down to none."),
            ("Emergency", "The full port. There is nothing deeper than no vacuum, but it gets there fastest."),
        ]),
        ("page",),
        ("h2", "The Engine's Own Brake."),
        ("p", "It stays air, in proportion to the pipe: about a quarter with the pipe at 15 in. The independent works it directly, up to 3.8 bar."),
        ("h2", "Bailing Off."),
        ("p", "With the train brake applied, click the independent lever, or press the bail-off key: the engine's brake lets go and the train's stays on. A further application puts it back by that much, and a release clears the bail."),
    ]),
    dict(number="7.4", title="At the car, and the settings", head="Vacuum Equipment. The Cars.", blocks=[
        ("h", "7.4. At the Car."),
        ("h2", "The Release Cord."),
        ("p", "The bleed handle is the car's release cord. Click it: air goes into the chamber, the car's brake comes off within seconds, and it stays off. Its chamber's air then has to be drawn out by the engine, and with the pipe lapped that air lets the pipe down a little. A vacuum car has no lever row."),
        ("h2", "Hand Brakes."),
        ("p", "They work as on the air-braked cars (chapter 4)."),
        ("h2", "Text by the Crosshair."),
        ("p", "Look at the train brake, the independent, the brake valve cut-out, or a steam engine's wheel, and the text names its position and the vacuum in inches."),
        ("h2", "Settings."),
        ("pos", [
            ("Equipment", "Vacuum puts every engine and car on it, at once."),
            ("DA valves", "The cars' direct admission valves. On by default."),
            ("Load braking", "Period: every car brakes with its empty force, as the wagons did. EmptyLoad: automatic."),
            ("Ejectors", "How loud the steam engines' ejectors are, 0 to 200 %; 100 by default."),
        ]),
    ]),
]


def main():
    if len(sys.argv) not in (1, 2, 3):
        sys.exit(__doc__)
    if len(sys.argv) == 3:
        Path(sys.argv[2]).mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as scratch:
        if len(sys.argv) > 1:
            mb.SHIP = Path(sys.argv[1]).resolve()
            mb.OUT = Path(sys.argv[2]).resolve() if len(sys.argv) == 3 else Path(scratch)
        mb.COVER_LINE = "European, American, and Vacuum Equipment"
        mb.CHAPTERS = mb.CHAPTERS + CHAPTERS
        # Seventeen lines on the contents page, and eight tabs down the edge.
        mb.ROW = 58
        mb.CHAPTERS[0] = dict(mb.CHAPTERS[0], tab="AIR BRAKE")
        mb.build()


if __name__ == "__main__":
    main()
