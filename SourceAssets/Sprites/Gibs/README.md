# Authored player gibs

The complete `GIBS.zip` supplied by the artist contains 151 PNGs for all ten
stock classes. `import_gibs.py` copies those PNGs unchanged into the stock
gameplay pack, writes their sprite definitions, and reads `Gibs Sheet.aseprite`
to generate `AuthoredPlayerGibPlacementCatalog.cs`. To repeat the import:

```text
python SourceAssets/Sprites/Gibs/import_gibs.py
```

The sheet has 32×32 idle-pose cells, with the character anchor at (16, 20).
Its nine layers separate the body parts. The importer matches each Red PNG
against its layer and records the part-center offset from that anchor. The
Blue variant uses the same offset and has the same dimensions. Gib sprites
remain at source resolution and render at 2×; the spawn point and art are
mirrored when the character faces left. Both local and network death paths use
these offsets.

| Supplied class | Gameplay class | Parts per team |
| --- | --- | ---: |
| Employer | civilian | 8 |
| Runner | scout | 8 |
| Rocketman | soldier | 9 |
| Firebug | pyro | 8 |
| Detonator | demoman | 9 |
| Overweight | heavy | 8 |
| Constructor | engineer | 9 |
| Healer | medic | 8 |
| Marksman | sniper | 9 |
| Infiltrator | spy | 6 |

The counts follow the supplied PNGs. In particular, Overweight and Healer
have no separate hat; Infiltrator has one top and one bottom leg segment.
No missing pieces are synthesized. Runner has one chest segment; the other
classes have two. Source suffix `1` is Red, `2` is Blue, and an unnumbered
part is shared by both teams. Sprite IDs follow
`PlayerGib{Class}{TeamIfColored}{Part}S`.

The sheet predates small pixel edits to the Runner hat and Healer gun PNGs.
Their bounds still match the sheet, so the importer uses those bounds for
placement and retains the newer PNG pixels. The other 80 Red parts match their
sheet layers pixel for pixel.

These pieces use the existing bouncing gib entities. Classes outside the
stock catalog retain their previous gib behavior. Runtime Quote/legacy Quote
does not receive the stock Civilian/Employer set.

`_EXTRA_.zip` supplies the three unchanged 32×32 frames of
`GibBloodExplosionS`. The `GibBlood` death event starts one short expanding
burst at the gib origin. It draws in the gameplay-effects pass before remains,
so the moving gib pieces appear above it. Blood-visual settings also govern
this burst.
