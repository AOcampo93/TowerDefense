using AlienInvaders.Core;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class WaveGeneratorTests
    {
        [Test]
        public void Portals_FollowFormula_AndAreCapped()
        {
            int[] expected = { 2, 2, 3, 3, 4, 4, 4, 4, 4, 4 };
            for (int n = 1; n <= expected.Length; n++)
            {
                Assert.AreEqual(expected[n - 1], WaveGenerator.Build(n, 1).Portals, $"wave {n}");
            }
        }

        [Test]
        public void Ships_GrowByTwo_AndOnlyAFewFlyAtOnce()
        {
            int[] inAir = { 5, 6, 7, 8, 9, 10, 10, 10 };
            for (int n = 1; n <= inAir.Length; n++)
            {
                WaveConfig wave = WaveGenerator.Build(n, 1);
                Assert.AreEqual(6 + 2 * (n - 1), wave.TotalShips, $"ships in wave {n}");
                Assert.AreEqual(inAir[n - 1], wave.MaxShipsInAir, $"ships in the air in wave {n}");
                Assert.LessOrEqual(wave.MaxShipsInAir, wave.TotalShips);
            }
        }

        [Test]
        public void Ships_RaidTheCityLessAsWavesGo_ButAlwaysOnce()
        {
            int[] runs = { 3, 3, 3, 3, 2, 2, 2, 2, 1, 1 };
            for (int n = 1; n <= runs.Length; n++)
            {
                Assert.AreEqual(runs[n - 1], WaveGenerator.Build(n, 1).CityRuns, $"city raids in wave {n}");
            }
            Assert.AreEqual(Balance.MinCityRuns, WaveGenerator.Build(50, 1).CityRuns);
        }

        [Test]
        public void TowerAttackers_GrowEveryThreeWaves_UpToFour()
        {
            int[] attackers = { 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4 };
            for (int n = 1; n <= attackers.Length; n++)
            {
                Assert.AreEqual(attackers[n - 1], WaveGenerator.Build(n, 1).MaxTowerAttackers, $"tower attackers in wave {n}");
            }
        }

        [Test]
        public void AimTime_NeverBelowMinimum()
        {
            WaveConfig late = WaveGenerator.Build(50, 1);
            Assert.AreEqual(Balance.MinAimTime, late.AimTimeFighter, 0.001f);
            Assert.AreEqual(2f, late.SpeedMultiplier, 0.001f);
            Assert.AreEqual(Balance.MaxFighterSpeedMultiplier, late.FighterSpeedMultiplier, 0.001f);
        }

        [Test]
        public void Fighters_GetFasterEveryWave_MoreThanBombers()
        {
            float previous = 0f;
            for (int n = 1; n <= 8; n++)
            {
                WaveConfig wave = WaveGenerator.Build(n, 1);
                Assert.Greater(wave.FighterSpeedMultiplier, previous, $"wave {n}");
                previous = wave.FighterSpeedMultiplier;
            }
            WaveConfig sixth = WaveGenerator.Build(6, 1);
            Assert.Greater(sixth.FighterSpeedMultiplier, sixth.SpeedMultiplier);
            var fighter = AlienInvaders.Core.Ships.ShipFactory.Create(ShipType.Fighter, System.Numerics.Vector3.Zero, sixth, new System.Random(1));
            Assert.AreEqual(Balance.FighterSpeed * sixth.FighterSpeedMultiplier, fighter.Speed, 0.001f);
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

        [Test]
        public void ClearedWave_RestsOnlyAFewSeconds()
        {
            var cycle = new WaveCycle(new WaveGenerator(1));
            int attacks = 0;
            cycle.OnAttackStarted += _ => attacks++;
            cycle.Tick(Balance.PeaceSeconds + 0.01f, 0);
            cycle.Tick(Balance.PortalOpeningSeconds + 0.01f, 0);
            cycle.Tick(10f, 3);
            cycle.Tick(0f, 0); // cleared quickly: the rest is just as short
            Assert.AreEqual(Balance.PeaceSeconds, cycle.PhaseTimeLeft, 0.01f);

            // From the empty sky to the next ships: a few seconds only.
            float rest = 0f;
            while (attacks < 2 && rest < 60f)
            {
                cycle.Tick(0.1f, 0);
                rest += 0.1f;
            }
            Assert.AreEqual(2, attacks);
            Assert.LessOrEqual(rest, 7f);
        }

        [Test]
        public void SlowWave_LetsTheNextOneIn_WhileItsShipsKeepFlying()
        {
            var cycle = new WaveCycle(new WaveGenerator(1));
            int cleared = 0;
            cycle.OnWaveCleared += _ => cleared++;
            cycle.Tick(Balance.PeaceSeconds + 0.01f, 0);
            cycle.Tick(Balance.PortalOpeningSeconds + 0.01f, 0);
            Assert.AreEqual(1, cycle.CurrentWave.Number);
            for (float time = 0f; time < Balance.MaxWaveSeconds + 0.5f; time += 0.1f)
            {
                cycle.Tick(0.1f, 2); // two ships never brought down
            }
            Assert.AreEqual(WavePhase.PortalsOpening, cycle.Phase);
            Assert.AreEqual(2, cycle.CurrentWave.Number);
            Assert.AreEqual(0, cleared);
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

    public class WeaponBoostTests
    {
        [Test]
        public void PowerUps_ComeFromSomeBombers_InBigWaves_OrACrowdedSky()
        {
            var boost = new WeaponBoost();
            for (int i = 0; i < 30; i++)
            {
                Assert.IsFalse(boost.RegisterKill(ShipType.Bomber, Balance.PowerUpFirstWave - 1, 2), "a small early wave gave a power up");
                Assert.IsFalse(boost.RegisterKill(ShipType.Fighter, Balance.PowerUpFirstWave + 3, 9), "a fighter gave a power up");
            }
            int drops = 0;
            for (int i = 0; i < 12; i++)
            {
                if (boost.RegisterKill(ShipType.Bomber, Balance.PowerUpFirstWave, 2))
                {
                    drops++;
                }
            }
            // Not every bomber: one in every Balance.BombersPerPowerUp.
            Assert.AreEqual(12 / Balance.BombersPerPowerUp, drops);
            Assert.Less(drops, 12);
            // A crowded sky helps even before that wave.
            var crowded = new WeaponBoost();
            int crowdedDrops = 0;
            for (int i = 0; i < Balance.BombersPerPowerUp; i++)
            {
                if (crowded.RegisterKill(ShipType.Bomber, 1, Balance.PowerUpCrowd))
                {
                    crowdedDrops++;
                }
            }
            Assert.AreEqual(1, crowdedDrops);
        }

        [Test]
        public void Boost_HitsHarder_ForAFewSeconds()
        {
            var boost = new WeaponBoost();
            int started = 0, ended = 0;
            boost.OnStarted += () => started++;
            boost.OnEnded += () => ended++;
            Assert.AreEqual(Balance.ShotDamage, boost.ShotDamage);

            boost.Collect();
            Assert.AreEqual(Balance.ShotDamage * Balance.BoostDamageMultiplier, boost.ShotDamage);
            boost.Tick(Balance.BoostSeconds - 1f);
            boost.Collect(); // a second power up only makes it last longer
            boost.Tick(Balance.BoostSeconds - 0.5f);
            Assert.IsTrue(boost.Active);
            boost.Tick(1f);
            Assert.IsFalse(boost.Active);
            Assert.AreEqual(Balance.ShotDamage, boost.ShotDamage);
            Assert.AreEqual((1, 1), (started, ended));
        }
    }
}
