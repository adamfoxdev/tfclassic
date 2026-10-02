using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class CombatTests
{
    static (Game g, Player shooter, Player victim) Duel(PlayerClassId shooterClass, float distance, PlayerClassId victimClass = PlayerClassId.Soldier)
    {
        var g = NewGame();
        var shooter = Human(g, Team.Red, shooterClass);
        var victim = Human(g, Team.Blue, victimClass);
        Place(g, shooter, new Vector3(0, 0, 800), yaw: MathF.PI);          // red shooter looks toward -Z
        Place(g, victim, new Vector3(0, 0, 800 - distance), yaw: 0);
        return (g, shooter, victim);
    }

    static void Aim(Player p, Vector3 at, bool fire, int slot = -1)
    {
        var d = at - p.Eye;
        p.Input = new PlayerInput
        {
            SelectSlot = slot,
            Fire = fire,
            Yaw = MathF.Atan2(d.X, d.Z),
            Pitch = MathF.Atan2(d.Y, MathF.Sqrt(d.X * d.X + d.Z * d.Z)),
        };
    }

    [Fact]
    public void ShotgunDamagesEnemyButNotTeammate()
    {
        var (g, shooter, victim) = Duel(PlayerClassId.Scout, 200);
        Aim(shooter, victim.Center, fire: true, slot: 1);
        Run(g, 0.5f);
        Assert.True(victim.Health < victim.Class.MaxHealth || victim.Armor < victim.Class.StartArmor);

        var (g2, s2, ally) = Duel(PlayerClassId.Scout, 200);
        ally.Team = Team.Red;
        Aim(s2, ally.Center, fire: true, slot: 1);
        Run(g2, 1);
        Assert.Equal(ally.Class.MaxHealth, ally.Health);
    }

    [Fact]
    public void WallsBlockBullets()
    {
        var g = NewGame();
        var shooter = Human(g, Team.Red, PlayerClassId.Scout);
        var victim = Human(g, Team.Blue, PlayerClassId.Soldier);
        // Shooter outside the red fortress, victim inside the hall behind the front wall.
        Place(g, shooter, new Vector3(-250, 0, 1100), yaw: 0);
        Place(g, victim, new Vector3(-250, 0, 1450), yaw: 0);
        Aim(shooter, victim.Center, fire: true, slot: 2);
        Run(g, 1);
        Assert.Equal(victim.Class.MaxHealth, victim.Health);
        Assert.Equal((float)victim.Class.StartArmor, victim.Armor, 0.01f);
    }

    [Fact]
    public void RocketExplodesOnTargetAndKills()
    {
        var (g, shooter, victim) = Duel(PlayerClassId.Soldier, 500, PlayerClassId.Scout);
        victim.Armor = 0;
        for (int i = 0; i < 60 * 6 && victim.Alive; i++)
        {
            Aim(shooter, victim.Position + new Vector3(0, 8, 0), fire: true);
            g.Tick(Dt);
        }
        Assert.False(victim.Alive);
        Assert.Equal(1, shooter.Frags);
        Assert.Contains(g.Events, e => e.Text.Contains("Rocket"));
    }

    [Fact]
    public void RocketJumpLaunchesSoldierUpward()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, p, new Vector3(0, 0, 700));
        // Look straight down and fire; rocket detonates at the feet.
        p.Input = new PlayerInput { SelectSlot = -1, Fire = true, Pitch = -1.5f, Yaw = 0 };
        float peak = 0;
        for (int i = 0; i < 90; i++)
        {
            g.Tick(Dt);
            peak = MathF.Max(peak, p.Position.Y);
            p.Input.Fire = i < 2;
        }
        Assert.True(peak > 100f, $"peak={peak}");
        Assert.True(p.Health < p.Class.MaxHealth || p.Armor < p.Class.StartArmor, "rocket jump should cost something");
        Assert.True(p.Alive);
    }

    [Fact]
    public void SniperChargeScalesDamage()
    {
        float Shot(float holdSeconds)
        {
            var (g, shooter, victim) = Duel(PlayerClassId.Sniper, 1200, PlayerClassId.HeavyWeapons);
            victim.Armor = 0;
            int hold = (int)(holdSeconds / Dt);
            for (int i = 0; i < hold; i++)
            {
                Aim(shooter, victim.Position + new Vector3(0, 30, 0), fire: true);
                g.Tick(Dt);
            }
            Aim(shooter, victim.Position + new Vector3(0, 30, 0), fire: false);
            g.Tick(Dt);
            return victim.Class.MaxHealth - victim.Health;
        }

        float quick = Shot(0.05f), full = Shot(2.5f);
        Assert.InRange(quick, 40f, 80f);
        Assert.InRange(full, 250f - 100f, 300f);
        Assert.True(full > quick * 3);
    }

    [Fact]
    public void FlamethrowerSetsTargetBurning()
    {
        var (g, shooter, victim) = Duel(PlayerClassId.Pyro, 120);
        Aim(shooter, victim.Center, fire: true);
        Run(g, 0.3f);
        Assert.True(victim.BurnTime > 0);
        shooter.Input.Fire = false;
        float hp = victim.Health;
        Run(g, 3);
        Assert.True(victim.Health < hp || victim.Armor < victim.Class.StartArmor);
    }

    [Fact]
    public void MedikitHealsTeammates()
    {
        var (g, medic, ally) = Duel(PlayerClassId.Medic, 40);
        ally.Team = Team.Red;
        ally.Health = 20;
        Aim(medic, ally.Center, fire: true, slot: 0);
        Run(g, 1.0f);
        Assert.True(ally.Health > 40f, $"hp={ally.Health}");
    }

    [Fact]
    public void PipebombsStickAndDetonateOnAltFire()
    {
        var (g, demo, victim) = Duel(PlayerClassId.Demoman, 300, PlayerClassId.Scout);
        victim.Armor = 0;
        demo.Input = new PlayerInput { SelectSlot = 2, Yaw = MathF.PI, Pitch = 0.1f };
        Run(g, 0.4f);
        demo.Input.SelectSlot = -1;
        demo.Input.Fire = true;
        g.Tick(Dt);
        demo.Input.Fire = false;
        Run(g, 3f);
        Assert.Contains(g.Projectiles, p => p.Kind == ProjectileKind.Pipe && p.Stuck);

        // Teleport the victim next to the pipe, then detonate.
        var pipe = g.Projectiles.First(p => p.Kind == ProjectileKind.Pipe);
        Place(g, victim, pipe.Position - new Vector3(0, 0, 30));
        demo.Input.AltFire = true;
        g.Tick(Dt);
        Assert.True(victim.Health < victim.Class.MaxHealth, "pipebomb should hurt");
        Assert.DoesNotContain(g.Projectiles, p => p.Kind == ProjectileKind.Pipe);
    }

    [Fact]
    public void DeathRespawnsAndResupplyRefills()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        g.Kill(p, null, "test");
        Assert.False(p.Alive);
        Run(g, 5.5f);
        Assert.True(p.Alive);
        Assert.Equal(p.Class.MaxHealth, p.Health);

        Run(g, 3.5f);   // spawn top-up puts the resupply locker on cooldown
        p.Ammo[(int)AmmoType.Rockets] = 0;
        p.Health = 10;
        Run(g, 0.2f);
        Assert.Equal(p.Class.MaxAmmo[(int)AmmoType.Rockets], p.Ammo[(int)AmmoType.Rockets]);
        Assert.Equal(p.Class.MaxHealth, p.Health);
    }
}
