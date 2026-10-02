# TF Classic (C#)

A Team Fortress Classic–style capture-the-flag game in C# / .NET 8, with **one map** (`2fort_lite`),
seven classes and bots to play against. Single player; no networking.

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
| Right mouse | detonate your pipebombs (Demoman) |
| `1` `2` `3` / wheel | switch weapon |
| `Tab` | scoreboard |
| `M` | class menu (applies on respawn, or instantly in your own resupply room) |
| `Esc` | quit |

### Rules

* Grab the enemy flag from its stand and bring it to **your own flag stand**. Your flag must be at home to capture.
* Dropped flags return after 30 s, or instantly when a teammate touches them.
* First team to **5 captures** wins; the match then restarts.
* Resupply lockers (inside each fortress and spawn building) refill health, armor and ammo.

### Classes

Scout, Soldier (rocket jumping works), Demoman (grenade launcher + sticky pipebombs), HWGuy, Sniper (charged
shots, headshots), Medic (medikit heals teammates), Pyro (flamethrower, burning).
**Not yet implemented:** Spy and Engineer (they need disguise/sentry-gun systems), concussion grenades,
detpacks, infection.

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
