using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Converts between the Core's System.Numerics vectors and Unity's vectors. The Core cannot
    /// reference UnityEngine, so every position crossing the Core/Game boundary goes through here.
    /// Both sides use the same axes (X = East, Y = Up, Z = North), so the conversion is a plain copy.
    /// </summary>
    public static class VectorExt
    {
        /// <summary>Core vector → Unity vector, used when a view needs a Core position.</summary>
        public static Vector3 ToUnity(this System.Numerics.Vector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        /// <summary>Unity vector → Core vector, used when gameplay input (e.g. a hit point) feeds the Core.</summary>
        public static System.Numerics.Vector3 ToCore(this Vector3 v)
        {
            return new System.Numerics.Vector3(v.x, v.y, v.z);
        }
    }
}
