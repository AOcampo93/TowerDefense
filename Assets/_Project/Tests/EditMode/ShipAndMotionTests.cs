using System;
using System.Numerics;
using AlienInvaders.Core;
using AlienInvaders.Core.Motion;
using AlienInvaders.Core.Ships;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class EnemyShipTests
    {
        private static EnemyShip NewFighter(int seed = 1) =>
            ShipFactory.Create(ShipType.Fighter, new Vector3(0f, 20f, 35f), WaveGenerator.Build(1, seed), new Random(seed));

        /// <summary>Runs the ship until it starts aiming (or gives up after 60 simulated seconds).</summary>
        private static void RunUntilAiming(EnemyShip ship)
        {
            for (int i = 0; i < 6000 && ship.Phase != ShipPhase.Aiming; i++)
            {
                ship.Tick(0.01f);
            }
        }

        [Test]
        public void Factory_CreatesSubclassesWithStats()
        {
            EnemyShip bomber = ShipFactory.Create(ShipType.Bomber, Vector3.Zero, WaveGenerator.Build(1, 1), new Random(1));
            Assert.IsInstanceOf<Bomber>(bomber);
            Assert.AreEqual(Balance.BomberHealth, bomber.Health);
            Assert.IsInstanceOf<Fighter>(NewFighter());
        }

        [Test]
        public void Fighter_SurvivesOneBolt_AndFallsToABurst()
        {
            EnemyShip ship = NewFighter();
            Assert.AreEqual(Balance.FighterHealth, ship.MaxHealth);
            ship.Hit(Balance.ShotDamage);
            Assert.IsTrue(ship.IsAlive);
            for (int i = 1; i < Balance.FighterHealth; i++)
            {
                ship.Hit(Balance.ShotDamage);
            }
            Assert.IsFalse(ship.IsAlive);
        }

        [Test]
        public void Ship_ReachesStation_AndAims()
        {
            EnemyShip ship = NewFighter();
            bool aimed = false;
            ship.OnAimStarted += _ => aimed = true;
            RunUntilAiming(ship);
            Assert.IsTrue(aimed);
        }

        [Test]
        public void Ships_GlideSmoothly_AndNeverFlyThroughTheTower()
        {
            const float Step = 0.02f;
            for (int seed = 1; seed <= 20; seed++)
            {
                foreach (ShipType type in new[] { ShipType.Fighter, ShipType.Bomber })
                {
                    Vector3 portal = EnemyShip.FromAzimuth(seed * 47f, 160f, 30f);
                    EnemyShip ship = ShipFactory.Create(type, portal, WaveGenerator.Build(3, seed), new Random(seed));
                    Vector3 previous = ship.Position;
                    float previousSpeed = 0f;
                    float closest = float.MaxValue;
                    // A minute of flight: several stations, shots and turns.
                    for (int i = 0; i < 3000; i++)
                    {
                        ship.Tick(Step);
                        float speed = Vector3.Distance(ship.Position, previous) / Step;
                        // Speed changes no faster than the ship's acceleration: no sudden starts, stops or swerves (the
                        // margin covers the last step before a stop, which ends short on the station).
                        Assert.LessOrEqual(Math.Abs(speed - previousSpeed), ship.Acceleration * Step * 1.5f + 0.5f);
                        closest = Math.Min(closest, new Vector2(ship.Position.X, ship.Position.Z).Length());
                        previous = ship.Position;
                        previousSpeed = speed;
                    }
                    Assert.Greater(closest, 25f, $"{type} with seed {seed} flew too close to the tower");
                }
            }
        }

        [Test]
        public void Ship_FiresAfterAimTime()
        {
            EnemyShip ship = NewFighter();
            float damage = 0f;
            ship.OnFire += (_, __, amount) => damage += amount;
            RunUntilAiming(ship);
            ship.Tick(ship.AimTime + 0.01f);
            Assert.AreEqual(Balance.FighterDamage, damage, 0.001f);
        }

        [Test]
        public void DestroyingWhileAiming_PreventsDamage()
        {
            EnemyShip ship = NewFighter();
            bool fired = false;
            ship.OnFire += (_, __, ___) => fired = true;
            RunUntilAiming(ship);
            ship.Hit(Balance.FighterHealth);
            ship.Tick(10f);
            Assert.IsFalse(fired);
            Assert.IsFalse(ship.IsAlive);
        }

        [Test]
        public void TargetPoint_IsAlwaysOnTheFacadeBelowThePlayer()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                Vector3 target = NewFighter(seed).TargetPoint;
                Assert.That(target.Y, Is.InRange(Balance.MinImpactHeight, Balance.MaxImpactHeight), $"seed {seed}");
                Assert.LessOrEqual(Math.Abs(target.X), Balance.TowerHalfWidth + 0.001f);
                Assert.LessOrEqual(Math.Abs(target.Z), Balance.TowerHalfWidth + 0.001f);
            }
        }

        [Test]
        public void AzimuthConversions_AreConsistent()
        {
            Vector3 east = EnemyShip.FromAzimuth(90f, 10f, 0f);
            Assert.AreEqual(10f, east.X, 0.001f);
            Assert.AreEqual(90f, EnemyShip.AzimuthOf(east), 0.01f);
        }
    }

    public class MotionTests
    {
        private class CountingOutput : IMotionOutput
        {
            public int Applied;
            public int Resets;
            public void Apply(MotionSample sample) => Applied++;
            public void ResetToLevel() => Resets++;
        }

        [Test]
        public void Bus_SendsToAllOutputs()
        {
            var bus = new MotionBus();
            var a = new CountingOutput();
            var b = new CountingOutput();
            bus.Register(a);
            bus.Register(b);
            bus.Register(a); // duplicates are ignored
            bus.Publish(MotionSample.Level);
            bus.ResetAll();
            Assert.AreEqual((1, 1, 1, 1), (a.Applied, b.Applied, a.Resets, b.Resets));
        }

        [Test]
        public void ThresholdDrop_LowersHeave()
        {
            var tower = new TowerIntegrity();
            var model = new TowerMotionModel(tower);
            tower.ReceiveDamage(TowerSide.West, 35f); // crosses 66 %
            MotionSample s = model.Tick(Balance.DropWarningSeconds + Balance.DropDuration + 0.01f);
            Assert.AreEqual(-Balance.DropDistances[0], s.Heave, 0.001f);
            Assert.Greater(s.Shake, 0f);
        }

        [Test]
        public void ThresholdDrop_WaitsForTheWarning()
        {
            var tower = new TowerIntegrity();
            var model = new TowerMotionModel(tower);
            tower.ReceiveDamage(TowerSide.West, 35f); // crosses 66 %
            MotionSample creaking = model.Tick(Balance.DropWarningSeconds * 0.5f);
            Assert.AreEqual(0f, creaking.Heave, 0.001f);
            Assert.Greater(creaking.Shake, 0f);
        }

        [Test]
        public void HitCrossingTwoThresholds_DropsBothDistances()
        {
            var tower = new TowerIntegrity();
            var model = new TowerMotionModel(tower);
            tower.ReceiveDamage(TowerSide.West, 70f); // 30 %: crosses 66 and 33 in one hit
            MotionSample s = model.Tick(Balance.DropWarningSeconds + Balance.DropDuration + 0.01f);
            float expected = -(Balance.DropDistances[0] + Balance.DropDistances[1]);
            Assert.AreEqual(expected, s.Heave, 0.001f);
        }

        [Test]
        public void Recorder_WritesInvariantJson()
        {
            var recorder = new MotionRecorder();
            recorder.Apply(new MotionSample(1.5f, -2.25f, -1f, 0.5f, 0.05f));
            string json = recorder.ToJson();
            StringAssert.Contains("\"pitch\":1.5", json);
            StringAssert.Contains("\"roll\":-2.25", json);
        }
    }
}
