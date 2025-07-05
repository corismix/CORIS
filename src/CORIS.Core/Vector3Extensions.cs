using System.Numerics;

namespace CORIS.Core
{
    /// <summary>
    /// Extension helpers bridging double-precision calculations with System.Numerics.Vector3
    /// </summary>
    public static class Vector3Extensions
    {
        /// <summary>
        /// Magnitude alias returning the vector length as double (for legacy code convenience).
        /// </summary>
        public static double Magnitude(this Vector3 v) => v.Length();

        /// <summary>
        /// Multiply a Vector3 by a double, internally casting to float.
        /// </summary>
        public static Vector3 Mul(this Vector3 v, double scalar) => v * (float)scalar;

        /// <summary>
        /// Divide a Vector3 by a double, internally casting to float.
        /// </summary>
        public static Vector3 Div(this Vector3 v, double scalar) => v / (float)scalar;
    }
}