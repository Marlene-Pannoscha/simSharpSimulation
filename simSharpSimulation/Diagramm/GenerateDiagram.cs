using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Zentrale Orchestrierung aller Diagramme inkl. gemeinsamer Hilfsfunktionen (Pfad, Linspace, Histogramm).
    // Diese Klasse ist verantwortlich für die Erstellung von Diagrammen aus den Simulationsdaten.
    // Sie verwendet die ScottPlot-Bibliothek, um verschiedene Aspekte der Simulation zu visualisieren,
    // wie z.B. Ankunftszeiten und Wartezeiten.
    internal static partial class GenerateDiagramme
    {
        private const string ImagesOrdner = "images";
        private static readonly Lazy<string> ProjektRoot = new(ErmittleProjektRoot);

        // Erstellt alle Diagramme analog zur SimPy-Version:
        // 1) Theorie: PDF + CDF der Normalverteilung für Patientenankünfte.
        // 2) Simulation vs. Theorie: Ein Histogramm der tatsächlichen Ankünfte im Vergleich zur theoretischen Verteilung.
        // 3) Histogramm der Wartezeiten beim Arzt.
        // 4) Histogramm der Wartezeiten bei der Schwester.
        // 5) Ein vergleichendes Diagramm der Wartezeiten (Arzt vs. Schwester).
        // 6) Histogramm der Gesamtprozesszeit (Eintritt bis Austritt).
        // 7) Zeitachse für einen einzelnen Patienten (vom Eintritt bis Austritt).
        // 8) Vergleichs-Zeitachse für 3-10 Patienten mit unterschiedlichen Prozesspfaden.
        public static void GeneriereDiagramme(
            IReadOnlyList<double> echteAnkunftszeiten,
            IReadOnlyList<double> wartezeiten,
            IReadOnlyList<double> wartezeitenMitTermin,
            IReadOnlyList<double> wartezeitenOhneTermin,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> gesamtprozesszeiten,
            IReadOnlyList<TagesHitMissPunkt> hitMissProTag,
            IReadOnlyList<string> traceData,
            IReadOnlyDictionary<PatientenTyp, List<double>> arztBehandlungszeitenNachTyp,
            IReadOnlyDictionary<PatientenTyp, List<double>> schwesternBehandlungszeitenNachTyp,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            Console.WriteLine("Generiere Diagramme (Wartezeiten, Ankünfte, Theorie, Gesamtprozesszeit)...");

            // Erzeugt eine lineare Sequenz von Zeitpunkten für die X-Achse der theoretischen Kurven.
            double[] x = Linspace(0, simulationsdauer, 500);

            // Berechnet die Wahrscheinlichkeitsdichtefunktion (PDF) für jeden Zeitpunkt.
            double[] pdf = x
                .Select(v => MathNet.Numerics.Distributions.Normal.PDF(erwartungswert, standardabweichung, v))
                .ToArray();

            // Berechnet die kumulative Verteilungsfunktion (CDF) für jeden Zeitpunkt.
            double[] cdf = x
                .Select(v => MathNet.Numerics.Distributions.Normal.CDF(erwartungswert, standardabweichung, v))
                .ToArray();

            // Ruft die Methoden zur Erstellung der einzelnen Diagramme auf.
            // [Diagramm 1] Theorie: PDF + CDF
            ErzeugeTheorieDiagramm(x, pdf, cdf, erwartungswert, standardabweichung);
            // [Diagramm 2] Simulation vs. Theorie: Ankünfte
            ErzeugeAnkuenfteVergleichsDiagramm(echteAnkunftszeiten, x, pdf, cdf, simulationsdauer, erwartungswert, standardabweichung);
            // [Diagramm 3] Wartezeiten beim Arzt
            ErzeugeWartezeitenDiagramm(wartezeiten, anzahlAerzte);
            // [Diagramm 4] Wartezeiten bei der Schwester
            ErzeugeSchwesternWartezeitenDiagramm(schwesternWartezeiten, anzahlSchwestern);
            // [Diagramm 5] Vergleich Arzt vs. Schwester
            ErzeugeWartezeitenVergleichsDiagramm(wartezeiten, schwesternWartezeiten, anzahlAerzte, anzahlSchwestern);
            // [Diagramm 6] Gesamtprozesszeit
            ErzeugeGesamtprozesszeitDiagramm(gesamtprozesszeiten);
            // [Diagramm 7] Zeitachse eines Patienten
            ErzeugePatientenZeitachsenDiagramm(traceData);
            // [Diagramm 8] Vergleich 3-10 Patienten (verschiedene Pfade)
            ErzeugeMehrpatientenVergleichsZeitachse(traceData);
            // [Diagramm 9] Arzt-Behandlungszeiten (Histogramm + PDF + CDF je Typ)
            ErzeugeArztBehandlungszeitenJeTyp(arztBehandlungszeitenNachTyp);
            // [Diagramm 10] Schwester-Behandlungszeiten (Histogramm + PDF + CDF je Typ)
            ErzeugeSchwesterBehandlungszeitenJeTyp(schwesternBehandlungszeitenNachTyp);
            // [Diagramm 11] Wartezeiten-Theorie (Exponential): mit Termin vs. ohne Termin
            ErzeugeWartezeitenTheorieExponentialDiagramm(wartezeitenMitTermin, wartezeitenOhneTermin);
            // [Diagramm 12] Hit/Miss pro Tag
            ErzeugeHitMissProTagDiagramm(hitMissProTag);
        }

        private static void ErzeugeArztBehandlungszeitenJeTyp(IReadOnlyDictionary<PatientenTyp, List<double>> arztBehandlungszeitenNachTyp)
        {
            foreach (var (typ, _, behandlungszeitArzt, _, _) in PatientenKonfiguration.TYPEN_VERTEILUNG)
            {
                if (!arztBehandlungszeitenNachTyp.TryGetValue(typ, out var werte) || werte.Count == 0)
                    continue;
                ErzeugeArztBehandlungszeitenPdfCdfDiagramm(werte, typ, behandlungszeitArzt);
            }
        }

        private static void ErzeugeSchwesterBehandlungszeitenJeTyp(IReadOnlyDictionary<PatientenTyp, List<double>> schwesternBehandlungszeitenNachTyp)
        {
            foreach (var (typ, _, _, behandlungszeitSchwester, _) in PatientenKonfiguration.TYPEN_VERTEILUNG)
            {
                if (!schwesternBehandlungszeitenNachTyp.TryGetValue(typ, out var werte) || werte.Count == 0)
                    continue;
                ErzeugeSchwesterBehandlungszeitenPdfCdfDiagramm(werte, typ, behandlungszeitSchwester);
            }
        }

        // Stellt sicher, dass der Ausgabeordner für die Bilder existiert und gibt den vollständigen Pfad für eine Datei zurück.
        private static string ErzeugeOutputPfad(string dateiname)
        {
            string imagesPfad = Path.Combine(ProjektRoot.Value, ImagesOrdner);
            Directory.CreateDirectory(imagesPfad);
            return Path.Combine(imagesPfad, dateiname);
        }

        private static string ErmittleProjektRoot()
        {
            string? vonCwd = FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj");
            if (!string.IsNullOrEmpty(vonCwd))
                return vonCwd;

            string? vonBinary = FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj");
            if (!string.IsNullOrEmpty(vonBinary))
                return vonBinary;

            // Fallback, falls die .csproj-Datei unerwartet nicht gefunden wird.
            return Directory.GetCurrentDirectory();
        }

        private static string? FindeOrdnerMitDatei(string startPfad, string dateiname)
        {
            var current = new DirectoryInfo(startPfad);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, dateiname)))
                    return current.FullName;
                current = current.Parent;
            }
            return null;
        }

        // Erzeugt eine Sequenz von gleichmäßig verteilten Zahlen über ein angegebenes Intervall.
        // Ähnlich wie np.linspace in Python/NumPy.
        private static double[] Linspace(double start, double end, int count)
        {
            if (count < 2)
                return new[] { start };
            double step = (end - start) / (count - 1);
            return Enumerable.Range(0, count).Select(i => start + i * step).ToArray();
        }

        // Berechnet die Daten für ein Histogramm aus einer Liste von Werten.
        private static (double[] counts, double[] centers, double binWidth) BuildHistogram(
            IReadOnlyList<double> values,
            int binCount,
            double min,
            double max)
        {
            if (binCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(binCount), "binCount muss größer als 0 sein.");

            if (max <= min)
                max = min + 1.0;

            double binWidth = (max - min) / binCount;
            double[] counts = new double[binCount];
            double[] centers = new double[binCount];
            // Berechnet die Mittelpunkte der Bins für die X-Achse.
            for (int i = 0; i < binCount; i++)
                centers[i] = min + (i + 0.5) * binWidth;
            // Zählt, wie viele Werte in jeden Bin fallen.
            foreach (double value in values)
            {
                if (value < min || value > max)
                    continue; // Ignoriert Werte außerhalb des Bereichs.
                int index = (int)((value - min) / binWidth);
                if (index == binCount)
                    index = binCount - 1; // Ordnet den Maximalwert dem letzten Bin zu.
                counts[index]++;
            }
            return (counts, centers, binWidth);
        }
    }
}
