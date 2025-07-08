using System;
using System.Collections.Generic;
using CORIS.Core;

namespace CORIS.Sim
{
    public class RocketBuilder
    {
        private readonly List<Piece> _availableParts;
        private readonly List<Piece> _vesselParts = new();

        public RocketBuilder(string partsCatalogPath)
        {
            _availableParts = PartCatalog.LoadParts(partsCatalogPath);
        }

        public void Run()
        {
            if (_availableParts.Count == 0)
            {
                Console.WriteLine("No parts available. Please check the parts catalog file.");
                return;
            }

            bool isBuilding = true;
            while (isBuilding)
            {
                DisplayVessel();
                DisplayPartMenu();

                Console.Write("\n> Select a part to add, or type 'done' to finish: ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (input == "done")
                {
                    isBuilding = false;
                }
                else if (int.TryParse(input, out int choice) && choice > 0 && choice <= _availableParts.Count)
                {
                    var partToAdd = _availableParts[choice - 1];
                    _vesselParts.Add(partToAdd);
                    Console.WriteLine($"\nAdded: {partToAdd.Name}. Press any key to continue...");
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine("\nInvalid selection. Press any key to try again...");
                    Console.ReadKey();
                }
            }

            Console.Clear();
            Console.WriteLine("\n--- Final Vessel Configuration ---");
            DisplayVessel(false);
            Console.WriteLine("\nRocket construction complete. Returning to main menu.");
        }

        private void DisplayVessel(bool clearConsole = true)
        {
            if (clearConsole) Console.Clear();
            Console.WriteLine("--- Your Vessel ---");
            if (_vesselParts.Count == 0)
            {
                Console.WriteLine("(Empty)");
            }
            else
            {
                for (int i = 0; i < _vesselParts.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}: {_vesselParts[i].Name}");
                }
            }
            Console.WriteLine("-------------------");
        }

        private void DisplayPartMenu()
        {
            Console.WriteLine("\n--- Available Parts ---");
            for (int i = 0; i < _availableParts.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {_availableParts[i].Name} (Mass: {_availableParts[i].Mass}kg)");
            }
            Console.WriteLine("-----------------------");
        }
    }
}
