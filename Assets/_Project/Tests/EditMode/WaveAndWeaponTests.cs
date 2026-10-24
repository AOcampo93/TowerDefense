using AlienInvaders.Core;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class WaveGeneratorTests
    {
        [Test]
        public void Portals_FollowFormula_AndAreCapped()
        {
            int[] expected = { 1, 1, 2, 2, 3, 3, 4, 4, 4, 4 };
            for (int n = 1; n <= expected.Length; n++)
            {
                Assert.AreEqual(expected[n - 1], WaveGenerator.Build(n, 1).Portals, $"wave {n}");
            }
        }

        [Test]
        public void Ships_GrowByTwo_AndOnlyAFewFlyAtOnce()
        {
            int[] inAir = { 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 8 };
            for (int n = 1; n <= inAir.Length; n++)
            {
                WaveConfig wave = WaveGenerator.Build(n, 1);
                Assert.AreEqual(3 + 2 * (n - 1), wave.TotalShips, $"ships in wave {n}");
                Assert.AreEqual(inAir[n - 1], wave.MaxShipsInAir, $"ships in the air in wave {n}");
                Assert.LessOrEqual(wave.MaxShipsInAir, wave.TotalShips);
            }
        }

        [Test]
        public void AimTime_NeverBelowMinimum()
        {
            WaveConfig late = WaveGenerator.Build(50, 1);
            Assert.AreEqual(Balance.MinAimTime, late.AimTimeFighter, 0.001f);
            Assert.AreEqual(2f, late.SpeedMultiplier, 0.001f);
        }

        [Test]
        public void SameSeed_GivesSamePortals()
        {
            WaveConfig a = WaveGenerator.Build(5, 1234);
            WaveConfig b = WaveGenerator.Build(5, 1234);
            var pa = WaveGenerator.PortalPositions(a);
            var pb = WaveGenerator.PortalPositions(b);
            for (int i = 0; i < pa.Count; i++)
            {
                Assert.AreEqual(pa[i].AzimuthDegrees, pb[i].AzimuthDegrees);
            }
        }

        [Test]
        public void Bombers_StartInWaveTwo()
        {
            Assert.IsFalse(WaveGenerator.ShipTypes(WaveGenerator.Build(1, 3)).Contains(ShipType.Bomber));
            Assert.IsTrue(WaveGenerator.ShipTypes(WaveGenerator.Build(2, 3)).Contains(ShipType.Bomber));
        }

        [Test]
        public void PortalPlacement_ConvertsToPosition()
        {
            var east = new PortalPlacement { AzimuthDegrees = 90f, ElevationDegrees = 0f, Distance = 40f };
            Assert.AreEqual(40f, east.ToPosition().X, 0.001f);
            Assert.AreEqual(0f, east.ToPosition().Z, 0.001f);

            var high = new PortalPlacement { AzimuthDegrees = 0f, ElevationDegrees = 30f, Distance = 40f };
            Assert.AreEqual(20f, high.ToPosition().Y, 0.001f);
            Assert.AreEqual(40f, high.ToPosition().Length(), 0.001f);
        }

        [Test]
        public void ShipTypes_CountMatchesWave()
        {
            WaveConfig wave = WaveGenerator.Build(6, 99);
            var types = WaveGenerator.ShipTypes(wave);
            Assert.AreEqual(wave.TotalShips, types.Count);
            Assert.IsTrue(types.Contains(ShipType.Bomber));
        }
    }

    public class WaveCycleTests
    {
        [Test]
        public void Cycle_GoesPeace_Portals_Attack_Peace()
        {
            var cycle = new WaveCycle(new WaveGenerator(1));
            int portals = 0, attacks = 0, cleared = 0;
            cycle.OnPortalsOpening += _ => portals++;
            cycle.OnAttackStarted += _ => attacks++;
            cycle.OnWaveCleared += _ => cleared++;

            cycle.Tick(Balance.PeaceSeconds + 0.01f, 0);
            Assert.AreEqual(WavePhase.PortalsOpening, cycle.Phase);

            cycle.Tick(Balance.PortalOpeningSeconds + 0.01f, 0);
            Assert.AreEqual(WavePhase.Attacking, cycle.Phase);

            cycle.Tick(1f, 5); // ships still alive
            Assert.AreEqual(WavePhase.Attacking, cycle.Phase);

            cycle.Tick(1f, 0); // all destroyed
            Assert.AreEqual(WavePhase.Peace, cycle.Phase);
            Assert.AreEqual((1, 1, 1), (portals, attacks, cleared));
        }
    }

    public class WeaponHeatTests
    {
        [Test]
        public void ContinuousFire_Overheats()
        {
            var weapon = new WeaponHeat();
            int shots = 0;
            for (int i = 0; i < 1000 && !weapon.Overheated; i++)
            {
                weapon.Tick(1f / Balance.ShotsPerSecond, triggerHeld: true);
                if (weapon.TryFire()) shots++;
            }
            Assert.IsTrue(weapon.Overheated);
            Assert.AreEqual((int)System.Math.Ceiling(1f / Balance.HeatPerShot), shots);
        }

        [Test]
        public void Overheat_LocksThenRecoversAtHalfHeat()
        {
            var weapon = new WeaponHeat();
            while (!weapon.Overheated)
            {
                weapon.Tick(1f, true);
                weapon.TryFire();
            }
            Assert.IsFalse(weapon.TryFire());
            weapon.Tick(Balance.OverheatLockSeconds + 0.01f, false);
            Assert.IsFalse(weapon.Overheated);
            Assert.AreEqual(Balance.HeatAfterOverheat, weapon.Heat, 0.001f);
        }

        [Test]
        public void HeldTrigger_OverheatsInAboutFourAndAHalfSeconds()
        {
            // Same loop as the game: one Tick per headset frame (72 Hz), firing whenever the weapon allows it.
            var weapon = new WeaponHeat();
            const float frame = 1f / 72f;
            float elapsed = 0f;
            while (!weapon.Overheated && elapsed < 10f)
            {
                weapon.Tick(frame, triggerHeld: true);
                weapon.TryFire();
                elapsed += frame;
            }
            Assert.IsTrue(weapon.Overheated);
            Assert.That(elapsed, Is.InRange(4f, 5f));
        }

        [Test]
        public void FireRate_IsLimited()
        {
            var weapon = new WeaponHeat();
            Assert.IsTrue(weapon.TryFire());
            Assert.IsFalse(weapon.TryFire()); // no time passed
        }
    }
}
