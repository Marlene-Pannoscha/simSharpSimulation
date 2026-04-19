
using System;
using System.IO;
using System.Linq;

namespace simSharpSimulation
{
    class Program
    {
        /* Einfache Gesamtidee des Programms:
        - Program.cs startet die Simulation.
        - PatientenProzess.cs beschreibt den Weg eines Patienten (Rezeption -> Schwester -> Arzt).
        - PatientenGenerator.cs bestimmt, wann neue Patienten ankommen.
        - SimulationsDaten.cs sammelt alle Wartezeiten und Ereignisse.
        - Am Ende werden Diagramme und eine Trace-Datei erzeugt.
        */
        // --- 5. HAUPTPROGRAMM (Setup & Start) ---
        static void Main(string[] args)
        {
            Console.WriteLine("--- Start der SimSharp-Klinik-Simulation ---");

            var daten = new SimulationsDaten();
            var simulation = new PatientenProzess(SimulationKonfiguration.RANDOM_SEED, daten);
            simulation.FuehreAus();

            Console.WriteLine("--- Ende der SimSharp-Simulation. ---");

            // --- 6. VISUALISIERUNG (Diagramme) ---
            GenerateDiagramme.GeneriereDiagramme(
                daten.EchteAnkunftszeiten,
                daten.Wartezeiten,
                daten.SchwesternWartezeiten,
                daten.Gesamtprozesszeiten,
                daten.TraceData,
                SimulationKonfiguration.SIMULATIONSDAUER,
                PatientenKonfiguration.ERWARTUNGSWERT,
                PatientenKonfiguration.STANDARDABWEICHUNG,
                ArztKonfiguration.ANZAHL_AERZTE,
                SchwesterKonfiguration.ANZAHL_SCHWESTERN);


            // --- 7. EXPORT IN TEXTDATEI ---
            Console.WriteLine("--- Speichere Trace-File: klinik_trace.txt ---");
            var traceWithHeader = daten.TraceData.ToList();
            // Kommentarzeilen mit IDs
            int anzahlAerzte = ArztKonfiguration.ANZAHL_AERZTE;
            int anzahlSchwestern = SchwesterKonfiguration.ANZAHL_SCHWESTERN;
            string arztKommentar = $"# Aerzte: {string.Join(", ", Enumerable.Range(1, anzahlAerzte).Select(i => $"Arzt{i}=ID {i}"))}";
            string schwesterKommentar = $"# Schwestern: {string.Join(", ", Enumerable.Range(1, anzahlSchwestern).Select(i => $"Schwester{i}=ID {i}"))}";
            traceWithHeader.Insert(0, schwesterKommentar);
            traceWithHeader.Insert(0, arztKommentar);
            traceWithHeader.Insert(2, "Zeit;EventTyp;PatientId;ArztId;SchwesterId");
            File.WriteAllLines("klinik_trace.txt", traceWithHeader);
            Console.WriteLine("--- Trace-File erfolgreich gespeichert. ---");
            
            double avgWartezeit = daten.DurchschnittlicheWartezeitArzt;
            double avgSchwesternWartezeit = daten.DurchschnittlicheWartezeitSchwester;
            double avgRezeptionsWartezeit = daten.DurchschnittlicheWartezeitRezeption;
            double avgGesamtprozesszeit = daten.DurchschnittlicheGesamtprozesszeit;
            Console.WriteLine($"Simulation beendet. {daten.EchteAnkunftszeiten.Count} Patienten empfangen.");
            Console.WriteLine($"Durchschnittliche Wartezeit (Rezeption): {avgRezeptionsWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Wartezeit (Schwester): {avgSchwesternWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Wartezeit (Arzt): {avgWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Gesamtprozesszeit: {avgGesamtprozesszeit:F2} Minuten");
        }
    }
}
