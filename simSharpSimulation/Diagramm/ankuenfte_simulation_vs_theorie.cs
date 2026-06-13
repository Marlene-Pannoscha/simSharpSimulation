using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeAnkuenfteVergleichsDiagramm(
            IReadOnlyList<double> echteAnkunftszeiten,
            IReadOnlyList<double> echteAnkunftszeitenMitTermin,
            IReadOnlyList<double> echteAnkunftszeitenOhneTermin,
            double[] x,
            double[] terminPdf,
            double[] terminCdf,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung)
        {
            int anzahlBins = 24;
            var (countsMitTermin, centers, balkenBreite) = BuildHistogram(echteAnkunftszeitenMitTermin, anzahlBins, 0, simulationsdauer);
            var (countsOhneTermin, _, _) = BuildHistogram(echteAnkunftszeitenOhneTermin, anzahlBins, 0, simulationsdauer);
            double offset = balkenBreite * 0.22;

            var plot = new ScottPlot.Plot(1000, 600);
            var barsMitTermin = plot.AddBar(countsMitTermin, centers.Select(c => c - offset).ToArray());
            barsMitTermin.BarWidth = balkenBreite * 0.4;
            barsMitTermin.FillColor = Color.LightSkyBlue;
            barsMitTermin.BorderColor = Color.Black;
            barsMitTermin.Label = "Simulation: mit Termin";

            var barsOhneTermin = plot.AddBar(countsOhneTermin, centers.Select(c => c + offset).ToArray());
            barsOhneTermin.BarWidth = balkenBreite * 0.4;
            barsOhneTermin.FillColor = Color.Orange;
            barsOhneTermin.BorderColor = Color.Black;
            barsOhneTermin.Label = "Simulation: ohne Termin";

            double[] skalierteTerminPdf = terminPdf
                .Select(v => v * echteAnkunftszeitenMitTermin.Count * balkenBreite)
                .ToArray();
            plot.AddScatter(x, skalierteTerminPdf, color: Color.DarkBlue, lineWidth: 3, label: "Theorie: Termin Normal-PDF");

            double freezeZeitpunkt = Math.Max(
                0.0,
                simulationsdauer - SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN);
            double mittlereZwischenankunftszeit = Math.Max(
                0.0001,
                PatientenKonfiguration.OHNE_TERMIN_MITTLERE_ZWISCHENANKUNFTSZEIT_MINUTEN);
            double poissonErwartungProBin = Program.SimulierteArbeitstage *
                balkenBreite /
                mittlereZwischenankunftszeit;
            double[] poissonLinie = x.Select(v => v <= simulationsdauer ? poissonErwartungProBin : 0.0).ToArray();
            plot.AddScatter(x, poissonLinie, color: Color.DarkOrange, lineWidth: 3, label: "Theorie: ohne Termin Poisson-Rate");
            double freezeLinieHoehe = Math.Max(
                poissonErwartungProBin,
                Math.Max(countsMitTermin.DefaultIfEmpty(0).Max(), countsOhneTermin.DefaultIfEmpty(0).Max()));
            plot.AddScatter(
                new[] { freezeZeitpunkt, freezeZeitpunkt },
                new[] { 0.0, freezeLinieHoehe * 1.1 },
                color: Color.DarkGreen,
                lineStyle: ScottPlot.LineStyle.Dot,
                lineWidth: 2,
                label: "Queue-Freeze / Aufnahmeprognose");

            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var terminCdfLine = plot.AddScatter(x, terminCdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 2, label: "Termin Normal-CDF");
            terminCdfLine.YAxisIndex = axisRight.AxisIndex;

            double[] poissonCdf = x
                .Select(v => simulationsdauer > 0.0 ? Math.Clamp(v / simulationsdauer, 0.0, 1.0) : 0.0)
                .ToArray();
            var poissonCdfLine = plot.AddScatter(x, poissonCdf, color: Color.SaddleBrown, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 2, label: "Ohne Termin kumulierte Rate");
            poissonCdfLine.YAxisIndex = axisRight.AxisIndex;

            plot.Title($"Simulation vs. Theorie: Ankuenfte\nMit Termin: Normal | Ohne Termin: Poisson bis Praxisschluss, Queue-Freeze bei Minute {freezeZeitpunkt:N0}");
            plot.XLabel("Zeit der Simulation in Minuten (0 bis 480)");
            plot.YLabel($"Patienten pro {balkenBreite:N0}-Minuten-Bin");
            axisRight.Label("Kumulierte Wahrscheinlichkeit / Rate");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);

            string outputPath = ErzeugeOutputPfad("ankuenfte_simulation_vs_theorie.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 2 gespeichert: {outputPath} ---");
        }
    }
}
