using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeAufnahmeprognoseDiagramm(
            IReadOnlyList<(DateTime Tag, double ZeitpunktMinuten, int AufnahmeKapazitaet)> pruefungen,
            IReadOnlyList<(DateTime Tag, double ZeitpunktMinuten, int PatientId, bool Zugelassen, int RestKapazitaet, string Entscheidungsart)> entscheidungen)
        {
            var plot = new ScottPlot.Plot(1400, 750);

            var tags = pruefungen
                .Select(p => p.Tag.Date)
                .Concat(entscheidungen.Select(e => e.Tag.Date))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (tags.Count == 0)
            {
                plot.AddText(
                    "Keine Aufnahmeprognose-Daten vorhanden.",
                    0.5,
                    0.5,
                    size: 20,
                    color: Color.DimGray);
                SpeichereAufnahmeprognose(plot);
                return;
            }

            double[] basisX = Enumerable.Range(0, tags.Count).Select(i => (double)i).ToArray();
            string[] labels = tags.Select(t => t.ToString("dd.MM")).ToArray();
            double width = 0.18;

            double[] zugelassen = ZaehleNachTag(tags, entscheidungen, e => e.Zugelassen);
            double[] freezeAbgewiesen = ZaehleNachTag(tags, entscheidungen, e => !e.Zugelassen && e.Entscheidungsart == "FreezeAbgewiesen");
            double[] spaeteNeuankuenfte = ZaehleNachTag(tags, entscheidungen, e => !e.Zugelassen && e.Entscheidungsart == "SpaeteAnkunftAbgewiesen");
            double[] aktivSpaeterAbgewiesen = ZaehleNachTag(tags, entscheidungen, e => !e.Zugelassen && e.Entscheidungsart == "AktivAbgewiesen");

            FuegeBalkenHinzu(plot, zugelassen, basisX.Select(x => x - 1.5 * width).ToArray(), width, Color.SeaGreen, "Beim Freeze zugelassen");
            FuegeBalkenHinzu(plot, freezeAbgewiesen, basisX.Select(x => x - 0.5 * width).ToArray(), width, Color.IndianRed, "Beim Freeze abgewiesen");
            FuegeBalkenHinzu(plot, spaeteNeuankuenfte, basisX.Select(x => x + 0.5 * width).ToArray(), width, Color.Peru, "Spaete Neuankuenfte");
            FuegeBalkenHinzu(plot, aktivSpaeterAbgewiesen, basisX.Select(x => x + 1.5 * width).ToArray(), width, Color.MediumPurple, "Aktiv spaeter abgewiesen");

            BeschrifteBalken(plot, zugelassen, basisX.Select(x => x - 1.5 * width).ToArray());
            BeschrifteBalken(plot, freezeAbgewiesen, basisX.Select(x => x - 0.5 * width).ToArray());
            BeschrifteBalken(plot, spaeteNeuankuenfte, basisX.Select(x => x + 0.5 * width).ToArray());
            BeschrifteBalken(plot, aktivSpaeterAbgewiesen, basisX.Select(x => x + 1.5 * width).ToArray());

            double gesamtZugelassen = zugelassen.Sum();
            double gesamtFreezeAbgewiesen = freezeAbgewiesen.Sum();
            double gesamtSpaet = spaeteNeuankuenfte.Sum();
            double gesamtAktiv = aktivSpaeterAbgewiesen.Sum();

            plot.Title(
                "Queue-Freeze und Aufnahmeprognose eine Stunde vor Praxisschluss\n" +
                $"Zugelassen: {gesamtZugelassen:N0} | Freeze-abgewiesen: {gesamtFreezeAbgewiesen:N0} | Spaete Neuankuenfte: {gesamtSpaet:N0} | Aktiv spaeter abgewiesen: {gesamtAktiv:N0}");
            plot.XLabel("Simulationstag");
            plot.YLabel("Patienten");
            plot.XTicks(basisX, labels);
            plot.XAxis.TickLabelStyle(fontSize: 13, rotation: 0);
            plot.YAxis.TickLabelStyle(fontSize: 13);
            plot.Legend(location: ScottPlot.Alignment.UpperRight);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.SetAxisLimits(yMin: 0);

            SpeichereAufnahmeprognose(plot);
        }

        private static double[] ZaehleNachTag(
            IReadOnlyList<DateTime> tags,
            IReadOnlyList<(DateTime Tag, double ZeitpunktMinuten, int PatientId, bool Zugelassen, int RestKapazitaet, string Entscheidungsart)> entscheidungen,
            Func<(DateTime Tag, double ZeitpunktMinuten, int PatientId, bool Zugelassen, int RestKapazitaet, string Entscheidungsart), bool> filter)
        {
            return tags
                .Select(tag => (double)entscheidungen.Count(e => e.Tag.Date == tag.Date && filter(e)))
                .ToArray();
        }

        private static void FuegeBalkenHinzu(
            ScottPlot.Plot plot,
            double[] werte,
            double[] positionen,
            double width,
            Color farbe,
            string label)
        {
            var bars = plot.AddBar(werte, positionen);
            bars.BarWidth = width;
            bars.FillColor = farbe;
            bars.BorderColor = Color.Black;
            bars.Label = label;
        }

        private static void BeschrifteBalken(ScottPlot.Plot plot, double[] werte, double[] positionen)
        {
            for (int i = 0; i < werte.Length; i++)
            {
                if (werte[i] <= 0.0)
                    continue;

                plot.AddText(
                    werte[i].ToString("N0"),
                    positionen[i],
                    werte[i] + 0.35,
                    size: 11,
                    color: Color.Black);
            }
        }

        private static void SpeichereAufnahmeprognose(ScottPlot.Plot plot)
        {
            string outputPath = ErzeugeOutputPfad("aufnahmeprognose.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 15 gespeichert: {outputPath} ---");
        }
    }
}
