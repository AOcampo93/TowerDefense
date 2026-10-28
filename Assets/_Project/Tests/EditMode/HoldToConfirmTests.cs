using AlienInvaders.Core;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class HoldToConfirmTests
    {
        // Fires at the gun's rate (10 bolts per second) for some seconds and says whether the control acted.
        private static bool Fire(HoldToConfirm control, float seconds)
        {
            bool acted = false;
            for (float t = 0f; t < seconds; t += 0.1f)
            {
                control.Hit();
                acted |= control.Tick(0.1f);
            }
            return acted;
        }

        [Test]
        public void SteadyFire_ActsAfterTheHoldTime()
        {
            var control = new HoldToConfirm(1.2f, 0.3f);
            Assert.IsFalse(Fire(control, 1f));
            Assert.IsTrue(Fire(control, 0.3f));
            Assert.IsTrue(control.Done);
        }

        [Test]
        public void OneStrayBolt_NeverActs_AndDrains()
        {
            var control = new HoldToConfirm(1.2f, 0.3f);
            control.Hit();
            for (int i = 0; i < 30; i++)
            {
                Assert.IsFalse(control.Tick(0.1f));
            }
            Assert.AreEqual(0f, control.Progress, 0.0001f);
        }

        [Test]
        public void ActsOnlyOnce_UntilReset()
        {
            var control = new HoldToConfirm(0.5f, 0.3f);
            Assert.IsTrue(Fire(control, 1f));
            Assert.IsFalse(Fire(control, 1f));
            control.Reset();
            Assert.AreEqual(0f, control.Progress);
            Assert.IsTrue(Fire(control, 1f));
        }
    }
}
