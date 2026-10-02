using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class InfectionTests
{
    // Blue medic at z=900 facing -Z; a Red victim stands 50 units ahead.
    static (Game g, Player medic, Player victim) Setup(PlayerClassId victimClass = PlayerClassId.Soldier)
    {
        var g = NewGame();
        var medic = Human(g, Team.Blue, PlayerClassId.Medic);
        var victim = Human(g, Team.Red, victimClass);
        Place(g, medic, new Vector3(0, 0, 900), yaw: MathF.PI);
        Place(g, victim, new Vector3(0, 0, 850));
        g.Tick(Dt);
        return (g, medic, victim);
    }

    static void Swing(Game g, Player p, Player at)
    {
        var to = at.Center - p.Eye;
        p.Input = new PlayerInput
        {
            SelectSlot = 0, Fire = true,
            Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
        };
        g.Tick(Dt);
        p.FireCooldown = 0;
        g.Tick(Dt);
        p.Input.Fire = false;
    }

    [Fact]
    public void MedikitOnAnEnemyInfectsThem()
    {
        var (g, medic, victim) = Setup();
        victim.Armor = 0;
        Swing(g, medic, victim);
        Assert.Same(medic, victim.InfectedBy);
        Assert.True(victim.Health < victim.Class.MaxHealth, "the bonesaw also hurts");
        Assert.Contains(g.Events, e => e.Text.Contains("infected"));
        Assert.Contains("infected", victim.Notice);
    }

    [Fact]
    public void MedicsAreImmune()
    {
        var (g, medic, victim) = Setup(PlayerClassId.Medic);
        Swing(g, medic, victim);
        Assert.False(victim.IsInfected);
    }

    [Fact]
    public void InfectionDrainsHealthIgnoringArmorAndCreditsTheMedic()
    {
        var (g, medic, victim) = Setup();
        victim.Armor = 200;
        victim.InfectedBy = medic;
        victim.InfectionTick = Game.InfectionInterval;
        float hp = victim.Health, armor = victim.Armor;
        Run(g, 4.1f);
        Assert.InRange(hp - victim.Health, 5.5f, 6.5f);          // two ticks of 3
        Assert.Equal(armor, victim.Armor);

        victim.Health = 2;
        Run(g, 2.2f);
        Assert.False(victim.Alive);
        Assert.Equal(1, medic.Frags);
        Assert.Contains(g.Events, e => e.Text.Contains("Infection"));
        Assert.False(victim.IsInfected);                           // dying clears it
    }

    [Fact]
    public void SpreadsToNearbyTeammatesOnly()
    {
        var (g, medic, victim) = Setup();
        var near = Human(g, Team.Red, PlayerClassId.Soldier);
        var far = Human(g, Team.Red, PlayerClassId.Soldier);
        var redMedic = Human(g, Team.Red, PlayerClassId.Medic);
        var bluePal = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, near, new Vector3(70, 0, 850));
        Place(g, far, new Vector3(600, 0, 850));
        Place(g, redMedic, new Vector3(-70, 0, 850));
        Place(g, bluePal, new Vector3(0, 0, 790));
        foreach (var p in new[] { victim, near, far, redMedic, bluePal }) p.Health = 5000;

        victim.InfectedBy = medic;
        victim.InfectionTick = Game.InfectionInterval;
        Run(g, 40f);

        Assert.True(near.IsInfected, "standing next to the carrier");
        Assert.False(far.IsInfected);
        Assert.False(redMedic.IsInfected, "medics are immune");
        Assert.False(bluePal.IsInfected, "the infector's own team is never infected");
    }

    [Fact]
    public void SpreadIsBlockedByWalls()
    {
        var g = NewGame();
        var medic = Human(g, Team.Blue, PlayerClassId.Medic);
        var victim = Human(g, Team.Red, PlayerClassId.Soldier);
        var behindWall = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, victim, new Vector3(-250, 0, 1285));            // just outside the front wall (z 1300)
        Place(g, behindWall, new Vector3(-250, 0, 1340));        // just inside
        victim.Health = behindWall.Health = 5000;
        victim.InfectedBy = medic;
        Run(g, 30f);
        Assert.False(behindWall.IsInfected);
    }

    [Fact]
    public void AFriendlyMedikitCuresEvenAtFullHealth()
    {
        var g = NewGame();
        var enemyMedic = Human(g, Team.Blue, PlayerClassId.Medic);
        var medic = Human(g, Team.Red, PlayerClassId.Medic);
        var patient = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, medic, new Vector3(0, 0, 900), yaw: MathF.PI);
        Place(g, patient, new Vector3(0, 0, 850));
        g.Tick(Dt);
        patient.InfectedBy = enemyMedic;
        Assert.Equal(patient.Class.MaxHealth, patient.Health);

        Swing(g, medic, patient);
        Assert.False(patient.IsInfected);
        Assert.Contains("cured", patient.Notice);
    }

    [Fact]
    public void ResupplyLockerAndRespawnCure()
    {
        var (g, medic, victim) = Setup();
        victim.InfectedBy = medic;
        Place(g, victim, new Vector3(0, 0, 1500));               // red hall: resupply room
        Run(g, 0.3f);
        Assert.False(victim.IsInfected);

        victim.InfectedBy = medic;
        g.Kill(victim, null, "test");
        Assert.False(victim.IsInfected);
        victim.InfectedBy = medic;
        g.Respawn(victim);
        Assert.False(victim.IsInfected);
    }

    [Fact]
    public void PlainMeleeDoesNotInfect()
    {
        var g = NewGame();
        var scout = Human(g, Team.Blue, PlayerClassId.Scout);
        var victim = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, scout, new Vector3(0, 0, 900), yaw: MathF.PI);
        Place(g, victim, new Vector3(0, 0, 850));
        g.Tick(Dt);
        Swing(g, scout, victim);
        Assert.False(victim.IsInfected);
    }

    [Fact]
    public void MedicBotsInfectEnemiesAndCureTeammates()
    {
        var g = NewGame(5);
        for (int i = 0; i < 3; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Medic);
            g.AddBot(Team.Blue, PlayerClassId.Medic);
        }
        for (int i = 0; i < 3; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Soldier);
            g.AddBot(Team.Blue, PlayerClassId.Scout);
        }

        var wasInfected = g.Players.ToDictionary(p => p, _ => false);
        int infections = 0, cures = 0;
        for (int i = 0; i < 60 * 300; i++)
        {
            g.Tick(Dt);
            foreach (var p in g.Players)
            {
                if (p.IsInfected && !wasInfected[p]) infections++;
                if (!p.IsInfected && wasInfected[p] && p.Alive) cures++;
                wasInfected[p] = p.IsInfected;
            }
        }
        Assert.True(infections >= 2, $"infections={infections}");
        Assert.True(cures >= 1, $"cures={cures}");
    }
}
