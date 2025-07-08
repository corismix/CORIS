using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;

namespace CORIS.Core
{
    // Represents the smallest functional unit (e.g., engine, tank section)
    public class Piece
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Mass { get; set; } = 0.0;
        public Dictionary<string, double>? Properties { get; set; } = new();
    }

    // Represents a part, composed of multiple pieces (e.g., an engine assembly)
    public class Part
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<Piece> Pieces { get; set; } = new();
        public double Mass => ComputeMass();

        private double ComputeMass()
        {
            double total = 0;
            foreach (var piece in Pieces)
                total += piece.Mass;
            return total;
        }
    }

    // Represents a vessel, composed of multiple parts
    public class Vessel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<Part> Parts { get; set; } = new();
        public double Mass => ComputeMass();

        public void RecalculateMass()
        {
            // The Mass property is dynamically calculated, so this method is for legacy compatibility.
        }

        private double ComputeMass()
        {
            double total = 0;
            foreach (var part in Parts)
                total += part.Mass;
            return total;
        }
    }

    // Represents a 3D vector (legacy - prefer System.Numerics.Vector3 for performance)
    public struct Vector3Legacy
    {
        public double X, Y, Z;
        public Vector3Legacy(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vector3Legacy operator +(Vector3Legacy a, Vector3Legacy b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3Legacy operator -(Vector3Legacy a, Vector3Legacy b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3Legacy operator *(Vector3Legacy a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static Vector3Legacy operator /(Vector3Legacy a, double s) => new(a.X / s, a.Y / s, a.Z / s);
        public double Magnitude() => Math.Sqrt(X * X + Y * Y + Z * Z);
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    // Double-precision 3D vector for orbital mechanics
    public struct Vector3d
    {
        public double X, Y, Z;
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static Vector3d operator /(Vector3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);
        public double Magnitude() => Math.Sqrt(X * X + Y * Y + Z * Z);
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    // Represents the physical state of a vessel for simulation (now 3D)
    public class VesselState
    {
        public System.Numerics.Vector3 Position { get; set; } = new System.Numerics.Vector3(0f, 0f, 0f); // meters
        public System.Numerics.Vector3 Velocity { get; set; } = new System.Numerics.Vector3(0f, 0f, 0f); // m/s
        public System.Numerics.Vector3 Acceleration { get; set; } = new System.Numerics.Vector3(0f, 0f, 0f); // m/s^2
        // Orientation (Euler angles in degrees)
        public System.Numerics.Vector3 Orientation { get; set; } = new System.Numerics.Vector3(0f, 90f, 0f); // (Yaw, Pitch, Roll)
        public System.Numerics.Vector3 AngularVelocity { get; set; } = new System.Numerics.Vector3(0f, 0f, 0f); // deg/s
        public float Mass { get; set; } = 0f; // kg
        // Double-precision orbital state
        public Vector3d OrbitalPosition { get; set; } = new Vector3d(0, 0, 0); // meters
        public Vector3d OrbitalVelocity { get; set; } = new Vector3d(0, 0, 0); // m/s
    }

    // Tracks fuel for a vessel during simulation
    public class FuelState
    {
        public double Fuel { get; set; } = 0.0;
    }

    // Stub for mod-first loading (to be implemented)
    public static class ModLoader
    {
        public static List<Part> LoadPartsFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Parts file not found: {jsonPath}");
            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Part>>(json, options) ?? new List<Part>();
        }
        public static List<Part> LoadPartsFromXml(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"Parts file not found: {xmlPath}");

            var serializer = new XmlSerializer(typeof(List<Part>));
            using var fileStream = new FileStream(xmlPath, FileMode.Open);
            var result = serializer.Deserialize(fileStream) as List<Part>;
            return result ?? new List<Part>();
        }
    }
}