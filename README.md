# TF Classic (C#)

A Team Fortress Classic–style capture-the-flag game in C# / .NET 8, with **one map** (`2fort_lite`),
all nine classes and bots to play against. Single player; no networking.

## Run it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and a GPU/driver with OpenGL 3.3.

```sh
dotnet run --project src/TFClassic.Game
dotnet run --project src/TFClassic.Game -- --bots 8 --team red --class sniper
```

Options: `--bots N` (players per team, default 6), `--team red|blue`, `--class <name>`, `--seed N`,
`--width W --height H`.

### Controls

| Input | Action |
| --- | --- |
| `W A S D` | move (Quake-style air strafing works) |
| Mouse | aim |
| `Space` | jump |
| Left mouse | fire (sniper: hold to charge, release to shoot) |
| Right mouse | Demoman: detonate pipebombs. Engineer: build a sentry (again to demolish it) |
| `1` `2` `3` / wheel | switch weapon |
| `V` / `C` | Demoman: set a detpack / cycle its fuse (5, 20, 50 s) |
| `Q` / `E` | hold to prime a frag / concussion grenade (cook it), release to throw |
| `Tab` | scoreboard |
| `F` / `G` | Spy: cycle disguise as an enemy class / feign death |
| `B` | Engineer: build a dispenser (again to demolish it) |
| `T` | Engineer: build teleporter entrance, then exit, then demolish the pair |
| `M` | class menu (applies on respawn, or instantly in your own resupply room) |
| `Esc` | quit |

**No mouse?** Everything is playable from the keyboard:

| Key | Action |
| --- | --- |
| Numpad `4` `6` / `8` `2` | turn left / right, look up / down (`5` re-centres the view) |
| Arrow keys, or Numpad `7` `9` | move forward/back and strafe |
| Numpad `0` | fire (hold; sniper charges while held) |
| Numpad `Enter` or `.` | alt-fire (detonate pipebombs, build/demolish sentry) |
| Numpad `+` / `-` | next / previous weapon |
| Numpad `1` / `3` | hold to prime a frag / concussion grenade |
| Numpad `*` / `/` | Spy: disguise / feign death. Engineer: `*` dispenser, `/` teleporters. Demoman: `*` detpack, `/` fuse |
| Numpad `1`-`8` | choose a class in the menu |

### Rules

* Grab the enemy flag from its stand and bring it to **your own flag stand**. Your flag must be at home to capture.
* Dropped flags return after 30 s, or instantly when a teammate touches them.
* First team to **5 captures** wins; the match then restarts.
* Resupply lockers (inside each fortress and spawn building) refill health, armor and ammo.

### Classes

Scout, Soldier (rocket jumping works), Demoman (grenade launcher + sticky pipebombs), HWGuy, Sniper (charged
shots, headshots), Medic (medikit heals teammates, infects enemies), Pyro (flamethrower, burning), Engineer (sentry gun, dispenser, teleporters), Spy (disguise, backstab, sabotage).

**Engineer / sentry gun:** right-click builds a sentry about 56 units in front of you for 130 metal (it takes 3 s to
come online). Hit your own sentry with the wrench to upgrade it (100 metal per level, up to level 3 which adds
rockets) or, once maxed, to repair and reload it (10 metal per swing). Sentries only shoot enemies, need line of
sight, and can be shot, burned, blown up or clubbed down; the owner is credited with their kills. One sentry per
engineer; resupply lockers only top metal up by 20 per visit. Bot engineers build one at their post and tend it.

**Infection (Medic):** the medikit is a weapon too: hitting an *enemy* with it does 20 damage and **infects** them.
An infected player loses 3 health every 2 s, *ignoring armor* (the Medic who infected them gets the kill), is
surrounded by green poison motes that everyone can see, and **spreads the disease** to teammates standing within
130 units in line of sight (40% per tick). Medics are immune. It's cured by any friendly Medic's medikit (hit your
teammate with it), by walking into a resupply locker, or by dying. A nearby Medic is a field hospital and an
infected player running into a crowd is a liability. Bot medics chase and infect enemies who come within reach
and tend infected or hurt teammates.

**Detpack (Demoman):** `C` picks the fuse (5, 20 or 50 s; HUD shows it), `V` sets the pack about 56 units in front of
you. It takes 3 s to arm (press `V` again in that window to pick it back up), then the fuse counts down and it goes
off for **600 damage in 330 units**: enough to wipe a flag room and destroy sentries, dispensers and teleporters in
range, blocked by walls like any explosion (knockback is capped so nobody is launched into orbit). The owner takes
the blast too if they stay near. **Counterplay:** any enemy who stands beside an armed pack for 3 s defuses it (the
progress bar is visible to everyone and drains if they step away), a spy's knife defuses it instantly, and shooting it
apart (40 HP) also stops it. You get one per life, refilled at lockers. A blinking LED speeds up as the fuse runs down
(only your team sees it on the radar). Bot demomen lay a 5 s pack when an enemy is at mid range, then back off.

**Grenades:** every class carries frag grenades (Soldier, Demoman and HWGuy 4, the rest 2); Scout (3) and Medic (2)
also carry concussion grenades. Hold `Q` (frag) or `E` (concussion) to **prime** it and release to throw; the fuse is
3 s and it keeps burning while you hold, so you can *cook* a grenade to make it go off on landing. Cook it too long
and it explodes in your hand; die while holding one and you drop it live. A frag does up to 110 damage in 150 units
(half to yourself, none to teammates, blocked by walls). A concussion grenade does **no damage**: it shoves everyone in
260 units, teammates and you included, and leaves them dizzy for up to 8 s (the view sways and shots land off
target). Throw one at your own feet while jumping to **concussion-jump** like a Scout. Lockers refill grenades. Bots
throw cooked frags at visible enemies at mid range (they don't use concussions yet).

**Spy:** `F` starts a 2 s disguise as the next enemy class. Once it completes, enemy sentries and bots won't target you
and enemy players see you as one of their own (class colour, name tag, radar dot). Attacking or taking damage blows it.
The knife kills in one hit from behind (40 damage from the front) and, used on an enemy sentry, **sabotages** it: the
sentry is disabled and self-destructs after 4 s unless its engineer hits it with the wrench. The tranquilizer gun slows
targets for 3 s. `G` feigns death for up to 10 s (fake kill-feed message and corpse; you can't move or shoot, and
sentries and bots ignore you; not allowed while carrying the flag). Bot spies disguise on spawn, stab anything that
comes close and sabotage sentries they walk past.

**Teleporters:** press `T` to build a teleporter *entrance* (100 metal), walk somewhere else and press `T` again to
build the linked *exit* (100 metal); with both standing, `T` demolishes the pair for 80 metal back. Teammates who step
onto a working entrance appear on your exit (3 s cooldown, flag carriers included); the exit is one-way, and
**enemies standing on the exit when someone arrives are telefragged**. Pads can be shot, burned or blown up,
sabotaged by spies, and repaired with the wrench (10 metal per 40 HP). A cyan beam marks the entrance and an orange
one the exit while the pair is ready. Bot engineers build a pair once their sentry is level 2 and they have the
metal (entrance outside their spawn building, exit at the front of their half), and attacking bots hop on it.

All engineer structures share one code path (`Structure`): shooting, burning, explosions (rockets and grenades
detonate on them), melee, spy sabotage and wrench repair behave identically for sentries, dispensers and teleporters.

**Not yet implemented:** caltrops and the class-specific grenades (napalm, MIRV, nail, gas, EMP). (Bots don't yet go out of their way to destroy enemy dispensers, though they do shoot sentries.)

## The map

Two mirrored fortresses either side of a river, joined by a railed bridge, with wading lanes under the
bank stairs. Each fortress has a front door, two side doors, a hall (resupply), a carpeted flag room, a roof
deck reached by outside stairs, and a separate spawn building beside it.

## Layout

```
src/TFClassic.Core   headless simulation – no rendering, fully unit-tested
  Aabb / World         box-soup collision, sweeps, raycasts, trigger zones
  Movement             ground friction, air accel, stair stepping, wall sliding
  Game                 players, weapons, projectiles, explosions, flags, scoring, respawn
  GameMap              the 2fort_lite geometry, spawns and bot waypoints (Red half is mirrored to Blue)
  Navigation/BotBrain  waypoint A*, target selection, aiming, role assignment (attackers/defenders)
src/TFClassic.Game   Raylib-cs client: renderer, HUD, menu, input, fixed 60 Hz timestep
tests/TFClassic.Tests xunit: physics, combat, CTF rules, map validity, full bot-vs-bot matches
```

```sh
dotnet test     # ~30 s; includes three simulated 10-minute bot matches
```

### Headless screenshots (for development)

The client can render a frame under a virtual display and exit:

```sh
xvfb-run -a env LIBGL_ALWAYS_SOFTWARE=1 dotnet run --project src/TFClassic.Game -- \
  --seed 3 --warmup 10 --screenshot out.png --at 0,700,-300,0,-35   # x,y,z,yawDeg,pitchDeg
```

## Design notes

* Units are Half-Life units (player hull 32×32×72, jump ≈ 45 units, step height 18).
* The world is axis-aligned boxes, so stairs instead of ramps; collision is a swept-AABB slide-move like Quake's.
* Bots only see the hand-authored waypoint graph; `MapTests.EveryWaypointLinkIsWalkable` checks every link
  against the real collision hull whenever the map changes.
