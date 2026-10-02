using System.Numerics;

namespace TFClassic.Core;

/// <summary>Every sound the game can ask for. The Core library only names them; the client decides how to play them.</summary>
public enum SoundId
{
    // weapons
    Shotgun, Nailgun, SuperNailgun, RocketLaunch, GrenadeLauncher, PipeLaunch, AssaultCannon, AutoRifle, SniperShot,
    Flame, Tranq, MeleeSwing, MeleeHit, Backstab, SentryFire,
    // blasts and effects
    Explosion, DetpackBlast, ConcussionBlast, NapalmWhoosh, EmpZap, GasHiss, NailSpray, CaltropScatter, Sabotage,
    GrenadePin, GrenadeThrow, GrenadeBounce, DetpackBeep,
    // players
    Hurt, Death, HitMarker, Jump, Land, Footstep, Respawn, Resupply,
    // capture the flag
    FlagTake, FlagCapture, FlagReturn, FlagDrop, MatchWin,
    // engineer, spy, medic
    BuildStart, Upgrade, Teleport, DispenserUse, Disguise, Feign, Infected, Cure, Heal,
    // grappling hook
    GrappleFire, GrappleHit,
}

/// <summary>A sound that happened in the world at a place. SourcePlayerId lets the client single out its own player.</summary>
public readonly record struct SoundEvent(SoundId Id, Vector3 Position, float Volume, float Range, int SourcePlayerId);
