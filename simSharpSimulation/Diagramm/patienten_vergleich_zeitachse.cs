using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 8 - Vergleichszeitachse von mehreren Patientenpfaden.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 8
        private static void ErzeugeMehrpatientenVergleichsZeitachse(IReadOnlyList<string> traceData)
        {
            const int minVergleichPatienten = 3;
            const int maxVergleichPatienten = 10;

            var events = new List<(double Zeit, string EventTyp, int PatientId)>();
            foreach (string line in traceData)
            {
                string[] parts = line.Split(';');
                if (parts.Length < 3)
                    continue;

                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zeit))
                    continue;
                if (!int.TryParse(parts[2], out int patientId))
                    continue;

                events.Add((zeit, parts[1], patientId));
            }

            if (events.Count == 0)
                return;

            static double? FindeZeit(List<(double Zeit, string EventTyp, int PatientId)> evs, string eventTyp)
            {
                var hit = evs.FirstOrDefault(x => x.EventTyp == eventTyp);
                return hit.EventTyp is null ? null : hit.Zeit;
            }

            var byPatient = events
                .GroupBy(e => e.PatientId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var patientEvents = g.OrderBy(x => x.Zeit).ToList();
                    var betritt = FindeZeit(patientEvents, "betritt_klinik");
                    var verlaesst = FindeZeit(patientEvents, "verlaesst_klinik");
                    return new PatientenPfadInfo
                    {
                        PatientId = g.Key,
                        HatTermin = g.Any(x => x.EventTyp == "hat_termin"),
                        HatSchwesterVorbereitung = g.Any(x => x.EventTyp == "benoetigt_schwester_vorbereitung"),
                        Betritt = betritt,
                        StartRezeption = FindeZeit(patientEvents, "betritt_rezeption"),
                        StartSchwester = FindeZeit(patientEvents, "betritt_schwesterzimmer"),
                        StartArzt = FindeZeit(patientEvents, "betritt_arztzimmer"),
                        RueckwegZurRezeptionNachArzt = FindeZeit(patientEvents, "betritt_rezeption_nach_arzt")
                            ?? FindeZeit(patientEvents, "geht_nach_arzt_zur_rezeption"),
                        Verlaesst = verlaesst
                    };
                })
                .Where(p => p.Betritt.HasValue && p.Verlaesst.HasValue)
                .ToList();

            if (byPatient.Count == 0)
                return;

            var kandidaten = new List<PatientenPfadInfo>();

            void AddFirstMatch(bool hatTermin, bool hatVorbereitung)
            {
                PatientenPfadInfo? match = byPatient.FirstOrDefault(p =>
                    p.HatTermin == hatTermin &&
                    p.HatSchwesterVorbereitung == hatVorbereitung &&
                    !kandidaten.Any(c => c.PatientId == p.PatientId));
                if (match != null)
                    kandidaten.Add(match);
            }

            AddFirstMatch(true, true);
            AddFirstMatch(true, false);
            AddFirstMatch(false, true);
            AddFirstMatch(false, false);

            foreach (var p in byPatient)
            {
                if (kandidaten.Count >= maxVergleichPatienten)
                    break;
                if (!kandidaten.Any(c => c.PatientId == p.PatientId))
                    kandidaten.Add(p);
            }

            if (kandidaten.Count < Math.Min(minVergleichPatienten, byPatient.Count))
            {
                foreach (var p in byPatient)
                {
                    if (kandidaten.Count >= Math.Min(minVergleichPatienten, byPatient.Count))
                        break;
                    if (!kandidaten.Any(c => c.PatientId == p.PatientId))
                        kandidaten.Add(p);
                }
            }

            if (kandidaten.Count == 0)
                return;

            double minZeit = kandidaten.Min(p => p.Betritt!.Value);
            double maxZeit = kandidaten.Max(p => p.Verlaesst!.Value);
            double zeitSpanne = Math.Max(maxZeit - minZeit, 1.0);
            double linkerPuffer = Math.Max(1.0, zeitSpanne * 0.05);
            double rechterPuffer = Math.Max(8.0, zeitSpanne * 0.40);

            int plotHoehe = Math.Min(1600, Math.Max(900, 220 + kandidaten.Count * 95));
            var plot = new ScottPlot.Plot(1500, plotHoehe);

            for (int i = 0; i < kandidaten.Count; i++)
            {
                var p = kandidaten[i];
                double y = i + 1;

                plot.AddScatter(
                    new[] { p.Betritt!.Value, p.Verlaesst!.Value },
                    new[] { y, y },
                    color: Color.Gray,
                    lineWidth: 2,
                    markerSize: 0,
                    label: null);

                string? eintrittLabel = i == 0 ? "Eintritt" : null;
                plot.AddPoint(p.Betritt!.Value, y, color: Color.DimGray, size: 10, label: eintrittLabel);
                if (p.StartRezeption.HasValue)
                {
                    string? rezeptionLabel = i == 0 ? "Rezeption" : null;
                    plot.AddPoint(p.StartRezeption.Value, y, color: Color.SteelBlue, size: 9, label: rezeptionLabel);
                }
                if (p.StartSchwester.HasValue)
                {
                    string? schwesterLabel = i == 0 ? "Schwester" : null;
                    plot.AddPoint(p.StartSchwester.Value, y, color: Color.MediumVioletRed, size: 9, label: schwesterLabel);
                }
                if (p.StartArzt.HasValue)
                {
                    string? arztLabel = i == 0 ? "Arzt" : null;
                    plot.AddPoint(p.StartArzt.Value, y, color: Color.DarkGreen, size: 9, label: arztLabel);
                }
                if (p.RueckwegZurRezeptionNachArzt.HasValue)
                {
                    string? rueckwegLabel = i == 0 ? "Rückweg Rezeption" : null;
                    plot.AddPoint(p.RueckwegZurRezeptionNachArzt.Value, y, color: Color.OrangeRed, size: 9, label: rueckwegLabel);
                }
                string? austrittLabel = i == 0 ? "Austritt" : null;
                plot.AddPoint(p.Verlaesst!.Value, y, color: Color.Black, size: 10, label: austrittLabel);

                string laneLabel = $"ID {p.PatientId} | {(p.HatTermin ? "mit Termin" : "ohne Termin")} | {(p.HatSchwesterVorbereitung ? "mit Schwester-Vorbereitung" : "ohne Schwester-Vorbereitung")}{(p.RueckwegZurRezeptionNachArzt.HasValue ? " | mit Rezeption nach Arzt" : "")}";
                plot.AddText(laneLabel, p.Verlaesst!.Value + 1.0, y, color: Color.Black);
            }

            double[] laneYs = Enumerable.Range(1, kandidaten.Count).Select(v => (double)v).ToArray();
            string[] laneLabels = kandidaten.Select(p => $"P{p.PatientId}").ToArray();
            plot.YTicks(laneYs, laneLabels);

            var legend = plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            legend.Orientation = ScottPlot.Orientation.Vertical;
            legend.FillColor = Color.White;
            legend.OutlineColor = Color.Black;

            plot.Title("Vergleich 10 Patienten: Prozesspfade (Rezeption → Schwester → Arzt)");
            plot.XLabel("Zeit in Minuten seit Tagesbeginn");
            plot.YLabel("Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.SetAxisLimits(xMin: minZeit - linkerPuffer, xMax: maxZeit + rechterPuffer, yMin: 0.7, yMax: kandidaten.Count + 1.1);

            string outputPath = ErzeugeOutputPfad("patienten_vergleich_zeitachse.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 8 gespeichert: {outputPath} ---");
        }

        private sealed class PatientenPfadInfo
        {
            public int PatientId { get; set; }
            public bool HatTermin { get; set; }
            public bool HatSchwesterVorbereitung { get; set; }
            public double? Betritt { get; set; }
            public double? StartRezeption { get; set; }
            public double? StartSchwester { get; set; }
            public double? StartArzt { get; set; }
            public double? RueckwegZurRezeptionNachArzt { get; set; }
            public double? Verlaesst { get; set; }
        }
    }
}
