
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
            KonfigurationJsonExport.LadeAlle();

            var daten = new SimulationsDaten();
            var simulation = new PatientenProzess(SimulationKonfiguration.RANDOM_SEED, daten);
            simulation.FuehreAus();

            Console.WriteLine("--- Ende der SimSharp-Simulation. ---");

            // --- 6. VISUALISIERUNG (Diagramme) ---
            GenerateDiagramme.GeneriereDiagramme(
                daten.EchteAnkunftszeiten,
                daten.Wartezeiten,
                daten.WartezeitenMitTermin,
                daten.WartezeitenOhneTermin,
                daten.SchwesternWartezeiten,
                daten.Gesamtprozesszeiten,
                daten.TraceData,
                daten.ArztBehandlungszeitenNachTyp,
                daten.SchwesternBehandlungszeitenNachTyp,
                SimulationKonfiguration.SIMULATIONSDAUER,
                PatientenKonfiguration.ERWARTUNGSWERT,
                PatientenKonfiguration.STANDARDABWEICHUNG,
                ArztKonfiguration.ANZAHL_AERZTE,
                SchwesterKonfiguration.ANZAHL_SCHWESTERN);


            // --- 7. EXPORT IN TEXTDATEI ---
            Console.WriteLine("--- Speichere Trace-File: klinik_trace.txt ---");
            var traceWithHeader = daten.TraceData.ToList();
            // Nur noch Heading-Zeile (keine Kommentarzeilen mehr)
            traceWithHeader.Insert(0, "Zeit;EventTyp;VonZustand;ZuZustand;PatientId;ArztId;SchwesterId");
            File.WriteAllLines("klinik_trace.txt", traceWithHeader);
            Console.WriteLine("--- Trace-File erfolgreich gespeichert. ---");
            
            double avgWartezeit = daten.DurchschnittlicheWartezeitArzt;
            double avgSchwesternWartezeit = daten.DurchschnittlicheWartezeitSchwester;
            double avgRezeptionsWartezeit = daten.DurchschnittlicheWartezeitRezeption;
            double avgGesamtprozesszeit = daten.DurchschnittlicheGesamtprozesszeit;
            double avgWartezeitMitTermin = daten.DurchschnittlicheWartezeitArztMitTermin;
            double avgWartezeitOhneTermin = daten.DurchschnittlicheWartezeitArztOhneTermin;
            double avgRezeptionMitTermin = daten.DurchschnittlicheWartezeitRezeptionMitTermin;
            double avgRezeptionOhneTermin = daten.DurchschnittlicheWartezeitRezeptionOhneTermin;
            double avgRezeptionBehMitTermin = daten.DurchschnittlicheBehandlungszeitRezeptionMitTermin;
            double avgRezeptionBehOhneTermin = daten.DurchschnittlicheBehandlungszeitRezeptionOhneTermin;
            double avgSchwesterMitTermin = daten.DurchschnittlicheWartezeitSchwesterMitTermin;
            double avgSchwesterOhneTermin = daten.DurchschnittlicheWartezeitSchwesterOhneTermin;
            double avgSchwesterBehMitTermin = daten.DurchschnittlicheBehandlungszeitSchwesterMitTermin;
            double avgSchwesterBehOhneTermin = daten.DurchschnittlicheBehandlungszeitSchwesterOhneTermin;
            double avgArztBehMitTermin = daten.DurchschnittlicheBehandlungszeitArztMitTermin;
            double avgArztBehOhneTermin = daten.DurchschnittlicheBehandlungszeitArztOhneTermin;
            double avgGesamtprozesszeitMitTermin = daten.DurchschnittlicheGesamtprozesszeitMitTermin;
            double avgGesamtprozesszeitOhneTermin = daten.DurchschnittlicheGesamtprozesszeitOhneTermin;

            int anzahlMitTermin = daten.GesamtprozesszeitenMitTermin.Count;
            int anzahlOhneTermin = daten.GesamtprozesszeitenOhneTermin.Count;
            Console.WriteLine($"Simulation beendet. {daten.EchteAnkunftszeiten.Count} Patienten empfangen.");
            Console.WriteLine($"Durchschnittliche Wartezeit (Rezeption): {avgRezeptionsWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Wartezeit (Schwester): {avgSchwesternWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Wartezeit (Arzt): {avgWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Gesamtprozesszeit: {avgGesamtprozesszeit:F2} Minuten");

            Console.WriteLine();
            Console.WriteLine("--- Vergleich mit Termin vs. ohne Termin (Wartezeiten, Behandlungszeiten & Gesamtprozess) ---");
            Console.WriteLine($"{"Gruppe",-12} | {"Anz",5} | {"Rezept.W",8} | {"Rezept.B",8} | {"Schwest.W",8} | {"Schwest.B",8} | {"Arzt.W",8} | {"Arzt.B",8} | {"GesamtΣ",8}");
            Console.WriteLine(new string('-', 116));

            double gesamtSumMitTermin = avgRezeptionMitTermin + avgRezeptionBehMitTermin
                + avgSchwesterMitTermin + avgSchwesterBehMitTermin
                + avgWartezeitMitTermin + avgArztBehMitTermin;
            double gesamtSumOhneTermin = avgRezeptionOhneTermin + avgRezeptionBehOhneTermin
                + avgSchwesterOhneTermin + avgSchwesterBehOhneTermin
                + avgWartezeitOhneTermin + avgArztBehOhneTermin;

            Console.WriteLine($"{"Mit Termin",-12} | {anzahlMitTermin,5} | {avgRezeptionMitTermin,8:F2} | {avgRezeptionBehMitTermin,8:F2} | {avgSchwesterMitTermin,8:F2} | {avgSchwesterBehMitTermin,8:F2} | {avgWartezeitMitTermin,8:F2} | {avgArztBehMitTermin,8:F2} | {gesamtSumMitTermin,8:F2}");
            Console.WriteLine($"{"Ohne Termin",-12} | {anzahlOhneTermin,5} | {avgRezeptionOhneTermin,8:F2} | {avgRezeptionBehOhneTermin,8:F2} | {avgSchwesterOhneTermin,8:F2} | {avgSchwesterBehOhneTermin,8:F2} | {avgWartezeitOhneTermin,8:F2} | {avgArztBehOhneTermin,8:F2} | {gesamtSumOhneTermin,8:F2}");

            Console.WriteLine();
            Console.WriteLine("--- Patienten-Typen: Verteilung + Wartezeiten ---");
            Console.WriteLine($"{"Typ",-10} | {"Anzahl",8} | {"Anteil (%)",10} | {"Arzt (min)",12} | {"Schwester (min)",17}");
            Console.WriteLine(new string('-', 69));
            int gesamtTypen = daten.PatientenTypZaehler.Values.Sum();
            foreach (var (typ, anzahl) in daten.PatientenTypZaehler)
            {
                double anteil = gesamtTypen > 0 ? (anzahl * 100.0 / gesamtTypen) : 0.0;
                double avgArztTyp = daten.DurchschnittlicheArztWartezeitNachTyp(typ);
                double avgSchwesterTyp = daten.DurchschnittlicheSchwesterWartezeitNachTyp(typ);
                Console.WriteLine($"{typ,-10} | {anzahl,8} | {anteil,10:F2} | {avgArztTyp,12:F2} | {avgSchwesterTyp,17:F2}");
            }
        }

    }
}
