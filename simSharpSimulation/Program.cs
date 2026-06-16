
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
        internal const int SimulierteArbeitstage = 10;

        // --- 5. HAUPTPROGRAMM (Setup & Start) ---
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase)))
            {
                SchreibeHilfe();
                return;
            }

            if (args.Any(a => string.Equals(a, "--finanz-wpf", StringComparison.OrdinalIgnoreCase)))
            {
                KonfigurationJsonExport.LadeAlle();
                FinanzWpfFenster.StarteFenster();
                return;
            }

            bool nurSimulation = args.Any(a => string.Equals(a, "--simulation-only", StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(a, "--no-images", StringComparison.OrdinalIgnoreCase));
            bool mitBildern = args.Any(a => string.Equals(a, "--with-images", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "--images", StringComparison.OrdinalIgnoreCase));

            if (nurSimulation && mitBildern)
            {
                Console.WriteLine("Bitte entweder '--simulation-only' oder '--with-images' verwenden, nicht beide gleichzeitig.");
                SchreibeHilfe();
                return;
            }

            // Ohne Flag bleibt der Konsolenlauf bewusst schlank: Simulation + Text/JSON, aber keine PNG-Erzeugung.
            bool bilderErzeugen = mitBildern && !nurSimulation;

            Console.WriteLine("--- Start der SimSharp-Klinik-Simulation ---");
            Console.WriteLine(bilderErzeugen
                ? "--- Modus: Simulation mit Diagrammen/Bildern ---"
                : "--- Modus: Nur Simulation ohne Diagramm-/Bilderzeugung ---");
            KonfigurationJsonExport.LadeAlle();

            var daten = new SimulationsDaten();
            var simulation = new PatientenProzess(SimulationKonfiguration.RANDOM_SEED, daten);
            simulation.FuehreAus();

            Console.WriteLine("--- Ende der SimSharp-Simulation. ---");

            // --- 6. VISUALISIERUNG (Diagramme) ---
            if (bilderErzeugen)
            {
                GenerateDiagramme.GeneriereDiagramme(
                    daten.EchteAnkunftszeiten,
                    daten.Wartezeiten,
                    daten.WartezeitenMitTermin,
                    daten.WartezeitenOhneTermin,
                    daten.SchwesternWartezeiten,
                    daten.Gesamtprozesszeiten,
                    daten.HitMissProTag,
                    daten.TraceData,
                    daten.ArztBehandlungszeitenNachTyp,
                    daten.SchwesternBehandlungszeitenNachTyp,
                    daten.RezeptionsBehandlungszeiten,
                    SimulationKonfiguration.SIMULATIONSDAUER,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG,
                    ArztKonfiguration.ANZAHL_AERZTE,
                    SchwesterKonfiguration.ANZAHL_SCHWESTERN);
            }
            else
            {
                Console.WriteLine("--- Diagramm-/Bilderzeugung uebersprungen. Fuer Bilder: dotnet run -- --with-images ---");
            }


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
            if (bilderErzeugen)
                FuehreAufnahmeprognoseMatplotlibAus(prognoseJsonPfad);

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
            var (finanzenPfad, gewinnPfad, kostenstrukturPfad) = bilderErzeugen
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
            Console.WriteLine($"Behandlungskosten: {finanzen.Kosten.Behandlungskosten:F2} €");
            Console.WriteLine($"Gesamtkosten: {finanzen.Kosten.Gesamtkosten:F2} €");
            Console.WriteLine($"Gewinn: {finanzen.Gewinn:F2} €");
        }

        private static void FuehreAufnahmeprognoseMatplotlibAus(string prognoseJsonPfad)
        {
            try
            {
                string projektOrdner = ErmittleProjektOrdner();
                string skriptPfad = Path.Combine(projektOrdner, "Diagramm", "aufnahmeprognose_matplotlib.py");
                string outputOrdner = Path.Combine(projektOrdner, "images");
                Directory.CreateDirectory(outputOrdner);
                string outputPfad = Path.Combine(outputOrdner, "aufnahmeprognose_matplotlib.png");

                if (!File.Exists(skriptPfad))
                {
                    Console.WriteLine($"--- Matplotlib-Skript nicht gefunden: {skriptPfad} ---");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add(skriptPfad);
                startInfo.ArgumentList.Add(Path.GetFullPath(prognoseJsonPfad));
                startInfo.ArgumentList.Add(outputPfad);

                using Process process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Python-Prozess konnte nicht gestartet werden.");
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"--- Matplotlib-Aufnahmeprognose gespeichert: {outputPfad} ---");
                    return;
                }

                Console.WriteLine($"--- Matplotlib-Aufnahmeprognose konnte nicht erzeugt werden (ExitCode {process.ExitCode}). ---");
                if (!string.IsNullOrWhiteSpace(standardOutput))
                    Console.WriteLine(standardOutput.Trim());
                if (!string.IsNullOrWhiteSpace(standardError))
                    Console.WriteLine(standardError.Trim());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--- Matplotlib-Aufnahmeprognose uebersprungen: {ex.Message} ---");
            }
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
