# Amogus player skin

Red and Blue source PNGs are the supplied 32×32 Amogus sprite frames, normalized to the
repository's standard `Red` / `Blue` and `body_N.png`, `legs_N.png`, `taunt_N.png`
layout. The original source ZIP is preserved outside the repository under the task's
`.local/quote-curly-restoration-20261001/Amogus-source.zip`. `Blu/` was normalized to `Blue/`; pixel data is copied
without palette edits or resampling.

The catalog composites legs before the body layer, with body 1 and legs 1 for idle,
then body/legs 2–9 for the eight run poses. Body 10 is a standalone authored corpse
frame used by the classic GG2 corpse path. Taunt 1–22 remain an ordered 22-frame action
strip. The weapon is the supplied full-canvas frame and uses the weapon pivot and run
bob below; the importer normalizes each pose offset, and runtime applies the offset once.
No dedicated jump, humiliation, intel, or portrait art was supplied; those sprite names
alias the matching run/base poses or taunt frames.

All authored positions are in source pixels. Canvas 32×32, origin `[16, 20]`, and
pixel scale 2 follow the player-skin source convention. The weapon pivot is `[19, 24]`;
run-pose weapon offsets are retained per source note and normalized once by the importer.

## Supplied sprite notes

- Frame 1: idle.
- Frames 2–9: run.
- Frame 10: corpse (authored DeadS, not a run-pose substitute).
- Render the weapon below the Impostor body because of the left-handed grip.
- Weapon rotation pivot: `[19, 24]`.
- Run weapon offsets: frames 2–9 are `[0,0]`, `[0,-1]`, `[0,-1]`, `[0,0]`, `[0,0]`, `[0,-1]`, `[0,-1]`, `[0,0]`.
