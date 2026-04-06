
using System;
using System.IO;
using System.Linq;

namespace simSharpSimulation
{
    class Program
    {
        // --- 5. HAUPTPROGRAMM (Setup & Start) ---
        static void Main(string[] args)
        {
            Console.WriteLine("--- Start der SimSharp-Klinik-Simulation ---");

            var daten = new SimulationsDaten();
            var simulation = new KlinikSimulation(SimulationKonfiguration.RANDOM_SEED, daten);
            simulation.FuehreAus();

            Console.WriteLine("--- Ende der SimSharp-Simulation. ---");

            // --- 6. VISUALISIERUNG (Diagramme) ---
            KlinikDiagramme.GeneriereDiagramme(
                daten.EchteAnkunftszeiten,
                daten.Wartezeiten,
                SimulationKonfiguration.SIMULATIONSDAUER,
                SimulationKonfiguration.ERWARTUNGSWERT,
                SimulationKonfiguration.STANDARDABWEICHUNG,
                SimulationKonfiguration.ANZAHL_AERZTE);

            // --- 7. EXPORT IN TEXTDATEI ---
            Console.WriteLine("--- Speichere Trace-File: klinik_trace.txt ---");
            File.WriteAllLines("klinik_trace.txt", daten.TraceData);
            Console.WriteLine("--- Trace-File erfolgreich gespeichert. ---");
            
            double avgWartezeit = daten.Wartezeiten.Count > 0 ? daten.Wartezeiten.Average() : 0;
            Console.WriteLine($"Simulation beendet. {daten.EchteAnkunftszeiten.Count} Patienten empfangen.");
            Console.WriteLine($"Durchschnittliche Wartezeit: {avgWartezeit:F2} Minuten");
        }
    }
}
