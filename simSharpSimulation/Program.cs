
using System;
using System.Diagnostics;
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
        internal const int SimulierteArbeitstage = 30;

        // --- 5. HAUPTPROGRAMM (Setup & Start) ---
        [STAThread]
        static void Main(string[] args)
        {
            bool vollstaendigerLauf = args.Any(a => string.Equals(a, "--mit-images", StringComparison.OrdinalIgnoreCase));

            if (args.Any(a => string.Equals(a, "--nur-simulationszeit", StringComparison.OrdinalIgnoreCase)))
            {
                KonfigurationJsonExport.LadeAlle();
                MesseUndSchreibeReineSimulationszeit();
                return;
            }

            if (args.Any(a => string.Equals(a, "--finanz-wpf", StringComparison.OrdinalIgnoreCase)))
            {
                KonfigurationJsonExport.LadeAlle();
                FinanzWpfFenster.StarteFenster();
                return;
            }

            Console.WriteLine(vollstaendigerLauf
                ? "--- Start der vollstaendigen SimSharp-Klinik-Simulation mit Diagrammen und Reports ---"
                : "--- Start der SimSharp-Klinik-Simulation ---");
            KonfigurationJsonExport.LadeAlle();

            var (daten, simulationszeit) = FuehreSimulationMitZeitmessungAus();

            Console.WriteLine("--- Ende der SimSharp-Simulation. ---");
            SchreibeReineSimulationszeit(simulationszeit);

            // --- 6. VISUALISIERUNG (Diagramme) ---
            GenerateDiagramme.GeneriereDiagramme(
                daten.EchteAnkunftszeiten,
                daten.Wartezeiten,
                daten.WartezeitenMitTermin,
                daten.WartezeitenOhneTermin,
                daten.SchwesternWartezeiten,
                daten.SchwesternWartezeitenMitTermin,
                daten.SchwesternWartezeitenOhneTermin,
                daten.RezeptionsWartezeiten,
                daten.Gesamtprozesszeiten,
                daten.HitMissProTag,
                daten.TraceData,
                daten.ArztBehandlungszeitenNachTyp,
                daten.SchwesternBehandlungszeitenNachTyp,
                daten.RezeptionsBehandlungszeiten,
                SimulationKonfiguration.SIMULATIONSDAUER,
                PatientenKonfiguration.ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN,
                PatientenKonfiguration.ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN,
                PatientenKonfiguration.ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN,
                ArztKonfiguration.ANZAHL_AERZTE,
                SchwesterKonfiguration.ANZAHL_SCHWESTERN);


            // --- 7. EXPORT IN TEXTDATEI ---
            Console.WriteLine("--- Speichere Trace-File: klinik_trace.txt ---");
            var traceWithHeader = daten.TraceData.ToList();
            // Nur noch Heading-Zeile (keine Kommentarzeilen mehr)
            traceWithHeader.Insert(0, "Zeit;EventTyp;VonZustand;ZuZustand;PatientId;ArztId;SchwesterId");
            File.WriteAllLines("klinik_trace.txt", traceWithHeader);
            Console.WriteLine("--- Trace-File erfolgreich gespeichert. ---");

            string prognosePfad = "prognose_report.txt";
            daten.SchreibePrognoseReport(prognosePfad);
            Console.WriteLine($"--- Prognose-Report gespeichert: {prognosePfad} ---");

            string prognoseJsonPfad = "prognose_daten.json";
            daten.SchreibePrognoseDatenJson(prognoseJsonPfad);
            Console.WriteLine($"--- Prognose-Daten gespeichert: {prognoseJsonPfad} ---");

            Console.WriteLine();
            Console.WriteLine(daten.ErzeugePrognoseReportText());
            
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
            var traceGesamtprozess = SimulationsTraceAuswertung.BerechneGesamtprozesszeitenNachTermin(daten.TraceData);
            double avgTraceGesamtprozesszeitMitTermin = traceGesamtprozess.MitTermin.Count > 0
                ? traceGesamtprozess.MitTermin.Average()
                : 0.0;
            double avgTraceGesamtprozesszeitOhneTermin = traceGesamtprozess.OhneTermin.Count > 0
                ? traceGesamtprozess.OhneTermin.Average()
                : 0.0;

            int anzahlMitTermin = daten.GesamtprozesszeitenMitTermin.Count;
            int anzahlOhneTermin = daten.GesamtprozesszeitenOhneTermin.Count;
            Console.WriteLine($"Simulation beendet. {daten.EchteAnkunftszeiten.Count} Patienten empfangen.");
            Console.WriteLine($"Durchschnittliche Wartezeit (Rezeption): {avgRezeptionsWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Wartezeit (Schwester): {avgSchwesternWartezeit:F2} Minuten");
            Console.WriteLine($"Durchschnittliche Wartezeit (Arzt): {avgWartezeit:F2} Minuten");
            Console.WriteLine($"Nicht behandelte Patienten gesamt: {daten.AnzahlNichtBehandeltRezeptionGesamt + daten.AnzahlNichtBehandeltSchwesterGesamt + daten.AnzahlNichtBehandeltArztGesamt}");
            Console.WriteLine($"  davon Rezeption-Schichtende: {daten.AnzahlNichtBehandeltRezeptionFeierabend}");
            Console.WriteLine($"  davon Schwester-Schichtende: {daten.AnzahlNichtBehandeltSchwesterFeierabend}");
            Console.WriteLine($"  davon Arzt-Schichtende: {daten.AnzahlNichtBehandeltArztFeierabend}");
            Console.WriteLine($"Durchschnittliche Gesamtprozesszeit: {avgGesamtprozesszeit:F2} Minuten");

            Console.WriteLine();
            Console.WriteLine("--- Vergleich mit Termin vs. ohne Termin (Wartezeiten, Behandlungszeiten & Gesamtprozess) ---");
            Console.WriteLine($"{"Gruppe",-12} | {"Anz",5} | {"Rezept.W",8} | {"Rezept.B",8} | {"Schwest.W",8} | {"Schwest.B",8} | {"Arzt.W",8} | {"Arzt.B",8} | {"GesamtΣ",8} | {"TraceGes",8}");
            Console.WriteLine(new string('-', 127));

            double gesamtSumMitTermin = avgRezeptionMitTermin + avgRezeptionBehMitTermin
                + avgSchwesterMitTermin + avgSchwesterBehMitTermin
                + avgWartezeitMitTermin + avgArztBehMitTermin;
            double gesamtSumOhneTermin = avgRezeptionOhneTermin + avgRezeptionBehOhneTermin
                + avgSchwesterOhneTermin + avgSchwesterBehOhneTermin
                + avgWartezeitOhneTermin + avgArztBehOhneTermin;

            Console.WriteLine($"{"Mit Termin",-12} | {anzahlMitTermin,5} | {avgRezeptionMitTermin,8:F2} | {avgRezeptionBehMitTermin,8:F2} | {avgSchwesterMitTermin,8:F2} | {avgSchwesterBehMitTermin,8:F2} | {avgWartezeitMitTermin,8:F2} | {avgArztBehMitTermin,8:F2} | {gesamtSumMitTermin,8:F2} | {avgTraceGesamtprozesszeitMitTermin,8:F2}");
            Console.WriteLine($"{"Ohne Termin",-12} | {anzahlOhneTermin,5} | {avgRezeptionOhneTermin,8:F2} | {avgRezeptionBehOhneTermin,8:F2} | {avgSchwesterOhneTermin,8:F2} | {avgSchwesterBehOhneTermin,8:F2} | {avgWartezeitOhneTermin,8:F2} | {avgArztBehOhneTermin,8:F2} | {gesamtSumOhneTermin,8:F2} | {avgTraceGesamtprozesszeitOhneTermin,8:F2}");
            Console.WriteLine("Hinweis: GesamtΣ ist die Summe der Einzelmittelwerte; TraceGes ist die echte End-to-End-Zeit aus dem Trace (betritt_klinik bis verlaesst_klinik).");

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

            Console.WriteLine();
            Console.WriteLine("--- Finanzen (Gesamtübersicht) ---");
            int behandeltePatientenGesamt = daten.Gesamtprozesszeiten.Count;
            int behandeltePatientenProTag = (int)Math.Round(behandeltePatientenGesamt / (double)SimulierteArbeitstage);
            int anzahlAerzte = ArztKonfiguration.ANZAHL_AERZTE;

            Console.WriteLine($"Simulierte Arbeitstage: {SimulierteArbeitstage}");
            Console.WriteLine($"Behandelte Patienten gesamt: {behandeltePatientenGesamt}");
            Console.WriteLine($"Behandelte Patienten pro Tag: {behandeltePatientenProTag}");
            Console.WriteLine($"Anzahl Ärzte: {anzahlAerzte}");

            // Erzeuge ein vollständiges Finanz-Ergebnis wie in der WPF-Ansicht und gib den identischen Bericht aus.
            FinanzErgebnis ergebnis = FinanzVisualisierung.Simuliere(anzahlAerzte, SchwesterKonfiguration.ANZAHL_SCHWESTERN, "Jahr");
            var (finanzenPfad, gewinnPfad, kostenstrukturPfad) = vollstaendigerLauf
                ? FinanzVisualisierung.ErzeugeDiagramme(ergebnis, anzahlAerzte, SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                : (
                    "nicht erzeugt (--simulation-only)",
                    "nicht erzeugt (--simulation-only)",
                    "nicht erzeugt (--simulation-only)");
            string reportText = FinanzVisualisierung.GenerateErgebnisReportText(ergebnis, finanzenPfad, gewinnPfad, kostenstrukturPfad);
            Console.WriteLine(reportText);

            Console.WriteLine("--- Finanzen (Tagesübersicht) ---");
            // Tagesübersicht (durchschnittlicher Tag)
            Tagesergebnis finanzenProTag = FinanzRechner.BerechneTagesergebnis(anzahlAerzte, behandeltePatientenProTag);
            Console.WriteLine($"Behandelte Patienten pro Tag: {behandeltePatientenProTag}");
            Console.WriteLine($"Anzahl Ärzte: {anzahlAerzte}");
            SchreibeFinanzwerte(finanzenProTag);
            Console.WriteLine();
            Console.WriteLine("Tipp: Für die Finanzansicht im extra Fenster verwende '--finanz-wpf'.");
        }

        private static void SchreibeHilfe()
        {
            Console.WriteLine("Verwendung:");
            Console.WriteLine("  dotnet run -- --simulation-only   Simulation ohne PNG-Diagramme/Bilder");
            Console.WriteLine("  dotnet run -- --with-images       Simulation mit allen Diagrammen/Bildern");
            Console.WriteLine("  dotnet run -- --finanz-wpf        Finanz- und Auswertungsfenster starten");
        }

        private static void SchreibeFinanzwerte(Tagesergebnis finanzen)
        {
            Console.WriteLine($"Umsatz: {finanzen.Umsatz:F2} €");
            Console.WriteLine($"Arztlohn: {finanzen.Kosten.Arztlohn:F2} €");
            Console.WriteLine($"Schwesterlohn: {finanzen.Kosten.Schwesterlohn:F2} €");
            Console.WriteLine($"Rezeptionlohn: {finanzen.Kosten.Rezeptionlohn:F2} €");
            Console.WriteLine($"Fixkosten: {finanzen.Kosten.Fixkosten:F2} €");
            Console.WriteLine($"Medizinisches Material: {finanzen.Kosten.MedizinischesMaterialkosten:F2} €");
            Console.WriteLine($"Gesamtkosten: {finanzen.Kosten.Gesamtkosten:F2} €");
            Console.WriteLine($"Gewinn: {finanzen.Gewinn:F2} €");
        }

        internal static string FormatiereDauer(TimeSpan dauer)
        {
            return dauer.TotalSeconds >= 1.0
                ? $"{dauer.TotalSeconds:N2} Sekunden"
                : $"{dauer.TotalMilliseconds:N0} ms";
        }

        private static void MesseUndSchreibeReineSimulationszeit()
        {
            Console.WriteLine("--- Starte reine Simulationszeitmessung ---");
            var (_, simulationszeit) = FuehreSimulationMitZeitmessungAus();
            SchreibeReineSimulationszeit(simulationszeit);
        }

        private static (SimulationsDaten Daten, TimeSpan Simulationszeit) FuehreSimulationMitZeitmessungAus()
        {
            var daten = new SimulationsDaten();
            var simulation = new PatientenProzess(SimulationKonfiguration.RANDOM_SEED, daten);
            Stopwatch simulationsStoppuhr = Stopwatch.StartNew();
            simulation.FuehreAus();
            simulationsStoppuhr.Stop();
            return (daten, simulationsStoppuhr.Elapsed);
        }

        private static void SchreibeReineSimulationszeit(TimeSpan simulationszeit)
        {
            Console.WriteLine($"Reine Simulationszeit (ohne Diagramm- und Dateierzeugung): {FormatiereDauer(simulationszeit)}");
        }

        private static string ErmittleProjektOrdner()
        {
            string? viaCwd = FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj");
            if (!string.IsNullOrEmpty(viaCwd))
                return viaCwd;

            string? viaBase = FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj");
            if (!string.IsNullOrEmpty(viaBase))
                return viaBase;

            return Directory.GetCurrentDirectory();
        }

        private static string? FindeOrdnerMitDatei(string startPfad, string dateiname)
        {
            DirectoryInfo? current = new(startPfad);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, dateiname)))
                    return current.FullName;

                current = current.Parent;
            }

            return null;
        }

    }
}
