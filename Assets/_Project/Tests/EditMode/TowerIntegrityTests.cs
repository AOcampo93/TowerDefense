using System.Collections.Generic;
using AlienInvaders.Core;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class TowerIntegrityTests
    {
        [Test]
        public void Damage_ReducesIntegrity()
        {
            var tower = new TowerIntegrity();
            tower.ReceiveDamage(TowerSide.North, 10f);
            Assert.AreEqual(90f, tower.Integrity, 0.001f);
        }

        [Test]
        public void Integrity_NeverGoesBelowZero()
        {
            var tower = new TowerIntegrity();
            tower.ReceiveDamage(TowerSide.East, 500f);
            Assert.AreEqual(0f, tower.Integrity, 0.001f);
            Assert.IsTrue(tower.IsDestroyed);
        }

        [Test]
        public void Thresholds_FireOnceAndInOrder_EvenWithOneBigHit()
        {
            var tower = new TowerIntegrity();
            var fired = new List<int>();
            tower.OnThresholdReached += fired.Add;

            tower.ReceiveDamage(TowerSide.North, 35f); // 65 % -> crosses 66
            tower.ReceiveDamage(TowerSide.North, 1f);  // 64 % -> nothing new
            tower.ReceiveDamage(TowerSide.South, 64f); // 0 % -> crosses 33 and 0

            CollectionAssert.AreEqual(new[] { 66, 33, 0 }, fired);
        }

        [Test]
        public void OppositeSides_CancelTilt()
        {
            var tower = new TowerIntegrity();
            tower.ReceiveDamage(TowerSide.North, 10f);
            tower.ReceiveDamage(TowerSide.South, 10f);
            Assert.AreEqual(0f, tower.TargetTilt.Length(), 0.001f);
        }

        [Test]
        public void Tilt_PointsTowardsDamagedSide_AndIsCappedByStage()
        {
            var tower = new TowerIntegrity();
            tower.ReceiveDamage(TowerSide.East, 20f); // 80 % integrity -> stage cap 7 degrees
            Assert.Greater(tower.TargetTilt.X, 0f);
            Assert.AreEqual(7f, tower.TargetTilt.Length(), 0.001f);
        }

        [Test]
        public void Tick_MovesTiltWithLimitedSpeed()
        {
            var tower = new TowerIntegrity();
            tower.ReceiveDamage(TowerSide.North, 20f);
            tower.Tick(0.5f); // max 5 deg/s -> 2.5 degrees after half a second
            Assert.AreEqual(2.5f, tower.CurrentTilt.Length(), 0.01f);
        }

        [TestCase(0f, TowerSide.North)]
        [TestCase(44f, TowerSide.North)]
        [TestCase(45f, TowerSide.East)]
        [TestCase(90f, TowerSide.East)]
        [TestCase(180f, TowerSide.South)]
        [TestCase(270f, TowerSide.West)]
        [TestCase(359f, TowerSide.North)]
        [TestCase(-90f, TowerSide.West)]
        public void SideFromAzimuth_MapsQuadrants(float azimuth, TowerSide expected)
        {
            Assert.AreEqual(expected, TowerIntegrity.SideFromAzimuth(azimuth));
        }
    }
}
