using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CORIS.Core
{
    /// <summary>
    /// Provides functionality to load rocket part definitions from a file.
    /// </summary>
    public static class PartCatalog
    {
        /// <summary>
        /// Loads a list of parts from a specified JSON file.
        /// </summary>
        /// <param name="filePath">The path to the parts JSON file.</param>
        /// <returns>A list of Piece objects.</returns>
        public static List<Piece> LoadParts(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Warning] Part catalog file not found at: {filePath}");
                return new List<Piece>();
            }

            try
            {
                var jsonString = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                var parts = JsonSerializer.Deserialize<List<Piece>>(jsonString, options);
                return parts ?? new List<Piece>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Error] Failed to parse part catalog file: {ex.Message}");
                return new List<Piece>();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Error] Failed to read part catalog file: {ex.Message}");
                return new List<Piece>();
            }
        }
    }
}
