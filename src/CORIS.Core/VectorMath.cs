using System;
using System.Numerics;

namespace CORIS.Core
{
    /// <summary>
    /// Vector math utilities to complement System.Numerics.Vector3
    /// Provides missing operations needed for high-performance physics and orbital mechanics
    /// </summary>
    public static class VectorMath
    {
        // Common vector constants
        public static readonly Vector3 Zero = new Vector3(0, 0, 0);
        public static readonly Vector3 UnitX = new Vector3(1, 0, 0);
        public static readonly Vector3 UnitY = new Vector3(0, 1, 0);
        public static readonly Vector3 UnitZ = new Vector3(0, 0, 1);

        /// <summary>
        /// Cross product of two Vector3
        /// </summary>
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        /// <summary>
        /// Transform a vector by a quaternion rotation
        /// </summary>
        public static Vector3 Transform(Vector3 vector, Quaternion rotation)
        {
            return Vector3.Transform(vector, rotation);
        }

        /// <summary>
        /// Distance between two points
        /// </summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            return (a - b).Length();
        }

        /// <summary>
        /// Normalize a vector safely (returns zero vector if input is zero)
        /// </summary>
        public static Vector3 SafeNormalize(Vector3 vector)
        {
            float length = vector.Length();
            return length > 1e-6f ? vector / length : Zero;
        }

        /// <summary>
        /// Linear interpolation between two vectors
        /// </summary>
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Spherical linear interpolation between two vectors
        /// </summary>
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            float dot = Vector3.Dot(a, b);

            // If the vectors are nearly identical, use linear interpolation
            if (Math.Abs(dot) > 0.9995f)
            {
                return Lerp(a, b, t);
            }

            float theta = (float)Math.Acos(Math.Abs(dot));
            float sinTheta = (float)Math.Sin(theta);

            float wa = (float)Math.Sin((1 - t) * theta) / sinTheta;
            float wb = (float)Math.Sin(t * theta) / sinTheta;

            return a * wa + b * wb;
        }

        /// <summary>
        /// Project vector a onto vector b
        /// </summary>
        public static Vector3 Project(Vector3 a, Vector3 b)
        {
            float bLengthSquared = b.LengthSquared();
            if (bLengthSquared < 1e-6f) return Zero;

            return b * (Vector3.Dot(a, b) / bLengthSquared);
        }

        /// <summary>
        /// Reject vector a from vector b (perpendicular component)
        /// </summary>
        public static Vector3 Reject(Vector3 a, Vector3 b)
        {
            return a - Project(a, b);
        }

        /// <summary>
        /// Reflect vector across a normal
        /// </summary>
        public static Vector3 Reflect(Vector3 vector, Vector3 normal)
        {
            return vector - normal * (2 * Vector3.Dot(vector, normal));
        }

        /// <summary>
        /// Calculate angle between two vectors in radians
        /// </summary>
        public static float Angle(Vector3 a, Vector3 b)
        {
            float denominator = (float)Math.Sqrt(a.LengthSquared() * b.LengthSquared());
            if (denominator < 1e-6f) return 0;

            float dot = Vector3.Dot(a, b) / denominator;
            return (float)Math.Acos(Math.Clamp(dot, -1.0f, 1.0f));
        }

        /// <summary>
        /// Signed angle between two vectors around an axis
        /// </summary>
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float unsignedAngle = Angle(from, to);
            float sign = Math.Sign(Vector3.Dot(axis, Cross(from, to)));
            return unsignedAngle * sign;
        }

        /// <summary>
        /// Rotate vector around an axis by angle in radians
        /// </summary>
        public static Vector3 RotateAroundAxis(Vector3 vector, Vector3 axis, float angle)
        {
            Quaternion rotation = Quaternion.CreateFromAxisAngle(axis, angle);
            return Transform(vector, rotation);
        }

        /// <summary>
        /// Clamp vector magnitude to maximum length
        /// </summary>
        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
        {
            float lengthSquared = vector.LengthSquared();
            if (lengthSquared > maxLength * maxLength)
            {
                float scale = maxLength / (float)Math.Sqrt(lengthSquared);
                return vector * scale;
            }
            return vector;
        }

        /// <summary>
        /// Move towards target vector at given speed
        /// </summary>
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 delta = target - current;
            float distance = delta.Length();

            if (distance <= maxDistanceDelta || distance < 1e-6f)
                return target;

            return current + delta / distance * maxDistanceDelta;
        }

        /// <summary>
        /// Smooth damp towards target (useful for camera following)
        /// </summary>
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity,
            float smoothTime, float maxSpeed = float.PositiveInfinity, float deltaTime = 0.02f)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            Vector3 change = current - target;
            Vector3 originalTo = target;

            float maxChange = maxSpeed * smoothTime;
            change = ClampMagnitude(change, maxChange);
            target = current - change;

            Vector3 temp = (currentVelocity + change * omega) * deltaTime;
            currentVelocity = (currentVelocity - temp * omega) * exp;
            Vector3 output = target + (change + temp) * exp;

            if (Vector3.Dot(originalTo - current, output - originalTo) > 0)
            {
                output = originalTo;
                currentVelocity = (output - originalTo) / deltaTime;
            }

            return output;
        }

        /// <summary>
        /// Convert from spherical coordinates (radius, theta, phi) to Cartesian
        /// </summary>
        public static Vector3 SphericalToCartesian(float radius, float theta, float phi)
        {
            float sinPhi = (float)Math.Sin(phi);
            return new Vector3(
                radius * sinPhi * (float)Math.Cos(theta),
                radius * (float)Math.Cos(phi),
                radius * sinPhi * (float)Math.Sin(theta)
            );
        }

        /// <summary>
        /// Convert from Cartesian to spherical coordinates
        /// Returns (radius, theta, phi)
        /// </summary>
        public static Vector3 CartesianToSpherical(Vector3 cartesian)
        {
            float radius = cartesian.Length();
            if (radius < 1e-6f) return Zero;

            float theta = (float)Math.Atan2(cartesian.Z, cartesian.X);
            float phi = (float)Math.Acos(cartesian.Y / radius);

            return new Vector3(radius, theta, phi);
        }
    }
}