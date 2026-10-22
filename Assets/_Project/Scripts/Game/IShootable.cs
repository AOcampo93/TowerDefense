namespace AlienInvaders.Game
{
    /// <summary>
    /// Anything the player's weapon can damage. The weapon only talks to this interface, so it does not need to
    /// know about ships (or any future target) to hit them.
    /// </summary>
    public interface IShootable
    {
        /// <summary>Applies the damage of one shot.</summary>
        void ReceiveHit(int damage);
    }
}
