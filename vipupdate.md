# Civilian / Employer VIP Update

## Locked Decisions

- The Civilian, also called the Employer, replaces Quote/Curly as the secret character selected with `Q` on class select.
- Keep the existing hidden `PlayerClass.Quote` runtime binding unless a later refactor makes a dedicated enum worthwhile.
- The umbrella primary fire behaves like a revolver for the base version.
- The umbrella secondary opens the umbrella, triggers a player-only airblast, and can be held open as a shield.
- The umbrella shield blocks bullets, rockets, grenades, stickies, needles, and similar weapon projectiles or hitscan shots.
- The umbrella shield does not block flames.
- The shield uses a slowly draining and recharging meter. If the player closes it before it breaks, it can be reused normally. If enemies break it, it cannot be used again until it fully recharges.
- The Civilian's taunt is also his spacebar special ability.
- The taunt heal affects the Civilian and allies for 30 HP.
- Money trails spawned while the Civilian walks heal teammates for 1 HP when touched and then disappear.
- VIP mode is activated by using `vip_` map names in map rotation, for example `vip_dirtbowl`.
- VIP maps reuse normal CP map geometry and objective data.
- On attack/defense CP maps, the VIP is assigned to the attacking team.
- On 5CP maps, each team receives one VIP.
- VIPs cannot switch classes or teams while VIP mode is active.
- If a VIP disconnects, try to select a replacement. If no valid replacement exists, end the round for the opposing team.
- VIP voting uses chat commands for the first pass.
- Vote pass threshold is over 50% majority, without requiring every eligible player to vote.
- Teams are force-swapped after VIP round end.
- Teams are force-shuffled after VIP map end.

## Implementation Checklist

- [x] Process and place Civilian run, taunt, umbrella, killfeed, and money assets.
- [x] Add Civilian/Employer to the stock/default package while preserving the packaged Quote/Curly content.
- [x] Fix hidden `Q` class rendering so Civilian run and taunt animations are used.
- [x] Add umbrella primary weapon as revolver-like behavior.
- [x] Add umbrella secondary ability, open animation state, shield meter, shield disable/recharge behavior, and player-only airblast.
- [x] Add umbrella shield blocking for non-flame hitscan/projectile weapon damage.
- [x] Add taunt healing for both normal taunt and spacebar special.
- [x] Add authoritative money trail pickups.
- [x] Add `vip_` map aliases and `GameModeKind.Vip`.
- [x] Add VIP selection state for A/D and 5CP maps.
- [x] Restrict VIP class and team switching.
- [x] Restrict point captures to VIP players in VIP mode.
- [x] Add VIP announcements using a center-screen modal style.
- [x] Add VIP round-end team swap and map-end team shuffle rules.
- [x] Generalize chat voting to pass at over 50% majority without full turnout.
- [x] Add VIP nomination/vote chat commands.
- [x] Add focused tests/updates for packaged Civilian asset/loadout loading, shield-related combat signature changes, VIP map aliases and mode state, and strict-majority VIP voting.
- [x] Correct Civilian default stance to use a neutral frame instead of the first run frame.
- [x] Replace the held-open umbrella frame with the full-open strip endpoint.
- [x] Add Civilian-specific umbrella HUD plaques for the primary ammo panel and shield meter.
- [x] Retune umbrella world offsets so the handle anchors at the Civilian's hand.
- [x] Add 40% slower falling while the umbrella is held open.
- [x] Buff umbrella shield capacity to roughly double the original charge.
- [x] Increase held-open umbrella fall slowdown to 50%.
- [x] Increase Civilian money trail spawn frequency and visual/pickup caps.
- [x] Make Civilian money pickups use body/intersection pickup bounds instead of a tiny origin-radius check.
- [x] Fix Civilian idle stand origin so the idle frame sits on the ground.
- [x] Fix stock Dirtbowl setup-gate import so lower setup gates use the full `SetupGateS` collision blocks.
- [x] Add a Practice setup VIP Rules toggle for CP maps, with setup-phase free class selection and bot VIP fallback at setup end.
- [x] Prevent random practice/server bots from taking Civilian in VIP contexts before the player can choose him.
- [x] Keep dead VIP slots assigned until VIP death resolution so the VIP is not demoted to Scout on death/respawn.

## Implementation Status

- Initial Civilian/Employer implementation is in place as stock gameplay class id `civilian`, using the existing hidden `PlayerClass.Quote` slot only for Q-select/protocol compatibility.
- Stock `stock.gg2` now owns the Civilian sprite definitions, source frames, umbrella item definitions, `ability.civilian-taunt`, and `civilian.stock` loadout.
- Packaged client and server Quote/Curly packs remain Quote/Curly content and no longer override the stock hidden Q binding.
- The umbrella primary is wired as a revolver-like weapon. The secondary opens the umbrella, triggers a player-only airblast, drains/recharges shield charge while held, and blocks non-flame weapon damage until broken.
- Civilian taunt healing is shared by normal taunt and the spacebar utility ability. It heals the Civilian and nearby same-team allies for 30 HP.
- Civilian money trail pickups are authoritative world pickups. Hurt same-team allies can consume them for 1 HP; the Civilian cannot consume his own trail.
- VIP mode is activated by `vip_` map names. `vip_dirtbowl` and `vip_dustbowl` alias the CP Dirtbowl geometry; `vip_egypt` loads as VIP and uses dual-VIP warmup behavior.
- VIP assignment supports single attacker VIP on setup A/D maps and one VIP per team on non-setup/5CP-style maps. VIPs are forced to Civilian, cannot switch class or team, and only VIPs can capture points.
- VIP disconnect/death handling attempts replacement through the normal assignment pass and ends the round for the opposing team if no valid replacement exists.
- VIP round transitions force team swaps after same-map rounds and team shuffles after map changes.
- VIP chat voting is implemented with `!votevip`, `!vipvote`, `!vote yes`, `!vote no`, `!vipstatus`, and `!vipcancel`. Passing requires a strict over-50% majority of eligible active players, not full turnout.
- Civilian idle, held-open umbrella, weapon HUD, and ability HUD assets have a follow-up correction pass: stand now uses taunt frame 0 as the neutral pose, held-open uses the full-open strip endpoint, and umbrella HUD art uses panel-sized Civilian-specific sprites.
- Holding the umbrella open now scales falling gravity and terminal fall speed to 50% of normal fall speed.
- The umbrella shield now has 360 charge ticks, up from 180, and the held-open fall-speed scale is 50% of normal.
- Civilian money trails now spawn much more frequently while moving, with higher authoritative pickup and visual caps so the trail reads as a constant drop instead of occasional bills.
- Civilian money pickups now use a 34x32 pickup marker against the ally's collision body, matching normal pickup behavior and making edge/body contact reliable. Full-health allies still do not consume money because no healing is applied.
- Civilian idle sprites have their stand origin moved down by 2 px to remove the idle-only hover above the ground.
- Stock GameMaker `ControlPointSetupGate` imports now use the original `SetupGateS` 32x32 full collision mask and only merge touching/overlapping stacked gate blocks. This restores Dirtbowl lower setup gates without merging separate upper/lower doors together.
- Practice setup now includes a `VIP Rules` toggle for CP maps. In practice CP setup, players can pick classes freely; once setup ends, an existing Civilian on the required team becomes VIP, otherwise an active non-local bot on that team is forced to Civilian as VIP.
- Practice VIP selection now prefers the local player's existing Civilian selection over Civilian bots on the same team, then falls back to the lowest-slot existing Civilian, then to a non-local bot only when no Civilian exists.
- Random client practice bots and server autofill bots no longer roll Civilian while VIP rules are active. Explicit/manual Civilian bot requests still work.
- Dead VIPs remain the assigned VIP until the VIP death rule resolves the round, preventing the dead Civilian from being treated as a non-VIP and forced back to Scout.

## Verification

- `dotnet build Core\OpenGarrison.Core.csproj --no-restore -v:minimal` passed.
- `dotnet build Server\OpenGarrison.Server.csproj --no-restore -v:minimal` passed.
- `dotnet build Client\OpenGarrison.Client.csproj --no-restore -v:minimal` passed and generated browser assets.
- Focused regression bundle passed: 81 tests covering the stock Civilian package/loadout, real Civilian sprite source loading, preserved Quote/Curly plugin behavior, and shipped Q-slot bot routes.
- Browser stock gameplay output was verified to contain `civilian`, `civilian.stock`, `ability.civilian-taunt`, `CivvieRedS`, `CivvieRedRunS`, `CivvieMoneyS`, and `CivvieUmbrellaKL`.
- Browser atlas pixel sampling verified non-transparent pixels for `CivvieRedS`, `CivvieRedRunS`, `CivvieMoneyS`, and `CivvieUmbrellaKL`.
- Follow-up sprite source sampling verified `CivvieRedS` matches `CivvieRedTauntS` frame 0, `CivvieUmbrellaOpenS` matches the full-open strip endpoints, and the new umbrella HUD plaque sprites are non-empty.
- Focused follow-up regression bundle passed: 50 tests covering gameplay pack loading, Civilian sprite source loading, VIP rules, and the umbrella fall-slow movement regression.
- `dotnet build Client\OpenGarrison.Client.csproj --no-restore -v:minimal` passed with browser atlas generation: 1105 sprites, 0 warnings.
- Focused setup/VIP/movement bundle passed: 11 tests covering Dirtbowl setup gates, VIP map rules, practice VIP assignment, and umbrella fall-slow movement.
- Focused Civilian package/performance bundle passed: 44 tests covering gameplay pack loading, Civilian performance regressions, and the adjusted Civilian presentation hit mask.
- `dotnet build Client\OpenGarrison.Client.csproj --no-restore -v:minimal -p:OpenGarrisonPackageScriptOwnsContent=true -p:UseSharedCompilation=false /nr:false /m:1` passed with 0 warnings.
- Focused VIP rules bundle passed: 9 tests covering VIP map aliases, practice VIP setup fallback, local-Civilian priority over Civilian bots, and dead-VIP class persistence.
- Focused Civilian money pickup bundle passed: 2 tests covering body-edge pickup healing and full-health non-consumption.
- Full no-build suite is not fully green in the current dirty worktree. Current residual failures are in unrelated bot/navigation areas: dynamic escort carrier route trace, Conflict shipped navigation fingerprint, and an intermittent server bot-manager scheduling assertion that passes when run in isolation.
- `git diff --check -- . ':!Modern/**'` passed with only existing LF-to-CRLF warnings.

## Investigation Notes

- `civ_run.zip` contains 16 transparent 64x64 frames: red frames 0-7 and blue frames 8-15.
- `civtaunt.zip` contains transparent 16-frame red and blue taunts. Frame names must be sorted numerically, not lexicographically.
- `v4.zip` contains 6-frame transparent red and blue umbrella-open sequences.
- `umbrella_open_v2.png` is a transparent 960x36 strip, likely 16 frames at 60x36.
- `killfeed1.png` and `killfeed2.png` are transparent 36x12 icons.
- The taunt money is embedded in the taunt frames; standalone money particles should be derived from those frames or hand-cropped from the bills.
- Quote/Curly support lives in the packaged `quote-curly.gg2` gameplay pack for both client and server, but it is de-bound from the legacy `PlayerClass.Quote` slot so it does not replace the stock Civilian.
- The class select `Q` path already selects `PlayerClass.Quote`, so the stock Civilian class binds that legacy slot while using `civilian` for package-facing content ids.
- Renderer code resolves presentation metadata from gameplay class ids, so the stock Civilian uses the `Civvie` sprite prefix instead of the old `Querly` fallback.
