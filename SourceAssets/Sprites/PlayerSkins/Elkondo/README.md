# Elkondo sprites

Source art by Elkondo, supplied in `runs.zip`. These are the author's selected
legs and torso layers, kept as separate PNGs and composed through
[PlayerSkins.json](../../../../Core/Content/PlayerSkins.json).

Each class has Red and Blue folders with an idle pose and eight run frames.
The import preserves the source PNGs and uses these explicit mappings:

- `detorun` maps to Demoman. Its Red torso frames came from `legs/Nowy folder`
  (the author's “new folder” note). Blue supplies a single `legs_blu/torso.png`,
  reused across the cycle with positions matching the Red torso's bob.
- Engineer's third frame is the supplied `3b.png`, named `3.png` here.
- Spy uses `legs` and `legs_blu`, with their torso layers. The cloaked body adds
  `knife/justKnife` or `knife_blu/justKnife`; the unused Spy experiments and
  animation previews are excluded.
- Where there is no separate idle torso, idle combines the supplied idle legs
  with torso frame 1. The catalog records that choice.
- Soldier's idle layers and equipment sit one source pixel lower to compensate
  for the transparent bottom row in its idle legs. Other poses keep their
  authored positions. All idle feet meet the same ground line as Kelly.

Jumps use the authored run cycle for all four stages: frame 1 pre-jump, frame 2
rising, frame 3 falling, and frame 4 landing. The archive has no separate jump
files or jump-frame metadata. The catalog assigns them explicitly, including the
cloaked Spy variant, so jumping does not switch back to the old body art. The
pre-jump and landing holds are slightly shorter than Kelly's equivalents.

Firing, reload, and backstab keep the existing weapon and dedicated action art.
Run frames use the same distance-based animation system as Kelly. Weapon and
equipment bob follows the torso's vertical movement; the Spy knife is baked
into the alternate body so it follows the same pose and cloak transparency.

To regenerate or verify the runtime sprites, use `scripts/import-player-skins.py`
or its `--check` option from the repository root with Python and Pillow installed.
