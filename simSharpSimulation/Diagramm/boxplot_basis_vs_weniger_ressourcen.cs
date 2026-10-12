using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

#pragma warning disable CA1416

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 16 - Boxplot-Vergleich Basis-Szenario gegen weniger Ressourcen.
    internal static partial class GenerateDiagramme
    {
        private const double BoxplotTagesNachlaufPufferMinuten = 180.0;
        private const double BoxplotSkalenbruchStart = 50.0;
        private const double BoxplotSkalenbruchEnde = 200.0;
        private const double BoxplotSkalenbruchAngezeigteSpanne = 24.0;
        private static readonly CultureInfo BoxplotCulture = CultureInfo.GetCultureInfo("de-DE");

        private static void ErzeugeBoxplotBasisVsWenigerRessourcen(
            IReadOnlyList<string> traceData,
            IReadOnlyList<double> rezeptionsWartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> arztWartezeiten,
            int behandeltePatientenGesamt)
        {
            RessourcenSzenario basis = new("Basis:1R/1S/2A", 1, 1, 2, 2, 1);
            RessourcenSzenario weniger = new("Weniger: 1R/1S/1A", 1, 1, 1, 1, 1);

            SzenarioBoxplotDaten basisDaten = ErzeugeBoxplotDatenAusVorhandenerSimulation(
                basis,
                traceData,
                rezeptionsWartezeiten,
                schwesternWartezeiten,
                arztWartezeiten,
                behandeltePatientenGesamt);
            SzenarioBoxplotDaten wenigerDaten = SimuliereBoxplotSzenario(weniger);

            ErzeugeSzenarioBoxplotDiagramm(
                "Diagramm 16: Boxplot Basis-Szenario vs. weniger Ressourcen",
                "Basis: 1 Rezeption, 1 Schwester, 2 Aerzte, 2 Arztzimmer, 1 Schwesterzimmer | weniger: 1 Arzt, 1 Arztzimmer",
                basisDaten,
                wenigerDaten,
                "boxplot_basis_vs_weniger_ressourcen.png",
                16,
                boxplotSkalenbruchEnde: 250.0);
        }

        private static SzenarioBoxplotDaten SimuliereBoxplotSzenario(RessourcenSzenario szenario)
        {
            int alteRezeption = RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN;
            int alteSchwestern = SchwesterKonfiguration.ANZAHL_SCHWESTERN;
            int alteAerzte = ArztKonfiguration.ANZAHL_AERZTE;
            int alteArztzimmer = KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt;
            int alteSchwesterzimmer = KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester;

            try
            {
                RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = szenario.Rezeptionisten;
                SchwesterKonfiguration.ANZAHL_SCHWESTERN = szenario.Schwestern;
                ArztKonfiguration.ANZAHL_AERZTE = szenario.Aerzte;
                KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt = szenario.Arztzimmer;
                KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester = szenario.Schwesterzimmer;

                SimulationsDaten daten = new();
                PatientenProzess simulation = new(SimulationKonfiguration.RANDOM_SEED, daten);
                simulation.FuehreAus();

                return ErzeugeBoxplotDatenAusSimulationsDaten(szenario, daten);
            }
            finally
            {
                RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = alteRezeption;
                SchwesterKonfiguration.ANZAHL_SCHWESTERN = alteSchwestern;
                ArztKonfiguration.ANZAHL_AERZTE = alteAerzte;
                KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt = alteArztzimmer;
                KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester = alteSchwesterzimmer;
            }
        }

        private static SzenarioBoxplotDaten ErzeugeBoxplotDatenAusSimulationsDaten(
            RessourcenSzenario szenario,
            SimulationsDaten daten)
        {
            return ErzeugeBoxplotDatenAusVorhandenerSimulation(
                szenario,
                daten.TraceData,
                daten.RezeptionsWartezeiten,
                daten.SchwesternWartezeiten,
                daten.Wartezeiten,
                daten.Gesamtprozesszeiten.Count);
        }

        private static SzenarioBoxplotDaten ErzeugeBoxplotDatenAusVorhandenerSimulation(
            RessourcenSzenario szenario,
            IReadOnlyList<string> traceData,
            IReadOnlyList<double> rezeptionsWartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> arztWartezeiten,
            int behandeltePatientenGesamt)
        {
            List<TagesBoxplotStatistik> tageswerte = BerechneTagesBoxplotStatistiken(
                traceData,
                szenario,
                behandeltePatientenGesamt);

            List<double> wartezeiten = rezeptionsWartezeiten
                .Concat(schwesternWartezeiten)
                .Concat(arztWartezeiten)
                .Where(wert => wert >= 0.0 && !double.IsNaN(wert) && !double.IsInfinity(wert))
                .ToList();

            return new SzenarioBoxplotDaten(
                szenario.Name,
                wartezeiten,
                tageswerte.Select(t => t.PersonalAuslastungProzent).ToList(),
                tageswerte.Select(t => t.RaumAuslastungProzent).ToList(),
                tageswerte.Select(t => t.Gesamtkosten).ToList(),
                new KostenKomponenten(
                    tageswerte.Count > 0 ? tageswerte.Average(t => t.Personalkosten) : 0.0,
                    tageswerte.Count > 0 ? tageswerte.Average(t => t.Fixkosten) : 0.0,
                    tageswerte.Count > 0 ? tageswerte.Average(t => t.Materialkosten) : 0.0),
                tageswerte.Select(t => t.DurchschnittlicheWarteschlangenlaenge).ToList());
        }

        private static List<TagesBoxplotStatistik> BerechneTagesBoxplotStatistiken(
            IReadOnlyList<string> traceData,
            RessourcenSzenario szenario,
            int behandeltePatientenGesamt)
        {
            Dictionary<int, List<BoxplotTraceEvent>> eventsNachTag = traceData
                .Select(ParseBoxplotTraceEvent)
                .Where(e => e is not null)
                .Select(e => e!)
                .GroupBy(e => e.TagIndex)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(e => e.Zeit).ThenBy(e => e.Index).ToList());

            int behandelteProTag = (int)Math.Round(behandeltePatientenGesamt / (double)Math.Max(1, Program.SimulierteArbeitstage));
            List<TagesBoxplotStatistik> result = new();
            for (int tag = 0; tag < Program.SimulierteArbeitstage; tag++)
            {
                eventsNachTag.TryGetValue(tag, out List<BoxplotTraceEvent>? events);
                TagesTraceStatistik statistik = BerechneTagesTraceStatistik(events ?? new List<BoxplotTraceEvent>());
                Tagesergebnis finanzen = FinanzRechner.BerechneTagesergebnis(
                    szenario.Aerzte,
                    szenario.Schwestern,
                    szenario.Rezeptionisten,
                    behandelteProTag);

                double personalKapazitaetsMinuten = (szenario.Rezeptionisten + szenario.Schwestern + szenario.Aerzte)
                    * SimulationKonfiguration.SIMULATIONSDAUER;
                double personalAuslastung = personalKapazitaetsMinuten > 0.0
                    ? (statistik.BelegtePersonalMinuten / personalKapazitaetsMinuten) * 100.0
                    : 0.0;
                double raumKapazitaetsMinuten = (szenario.Arztzimmer + szenario.Schwesterzimmer)
                    * SimulationKonfiguration.SIMULATIONSDAUER;
                double raumAuslastung = raumKapazitaetsMinuten > 0.0
                    ? (statistik.BelegteRaumMinuten / raumKapazitaetsMinuten) * 100.0
                    : 0.0;

                result.Add(new TagesBoxplotStatistik(
                    Math.Round(personalAuslastung, 2),
                    Math.Round(raumAuslastung, 2),
                    Math.Round(finanzen.Kosten.Gesamtkosten, 2),
                    Math.Round(finanzen.Kosten.Personalkosten, 2),
                    Math.Round(finanzen.Kosten.Fixkosten, 2),
                    Math.Round(finanzen.Kosten.MedizinischesMaterialkosten, 2),
                    Math.Round(statistik.DurchschnittlicheWarteschlangenlaenge, 2)));
            }

            return result;
        }

        private static TagesTraceStatistik BerechneTagesTraceStatistik(IReadOnlyList<BoxplotTraceEvent> events)
        {
            HashSet<int> rezeptionQueue = new();
            HashSet<int> schwesterQueue = new();
            HashSet<int> arztQueue = new();
            HashSet<int> rezeptionBelegt = new();
            HashSet<int> schwesterBelegt = new();
            HashSet<int> arztBelegt = new();
            HashSet<int> schwesterzimmerBelegt = new();
            HashSet<int> arztzimmerBelegt = new();

            double letzteZeit = 0.0;
            double queueMinuten = 0.0;
            double belegtePersonalMinuten = 0.0;
            double belegteRaumMinuten = 0.0;

            foreach (BoxplotTraceEvent traceEvent in events)
            {
                double zeit = Math.Clamp(traceEvent.Zeit, 0.0, SimulationKonfiguration.SIMULATIONSDAUER + BoxplotTagesNachlaufPufferMinuten);
                double dauer = Math.Max(0.0, zeit - letzteZeit);
                queueMinuten += dauer * (rezeptionQueue.Count + schwesterQueue.Count + arztQueue.Count);
                belegtePersonalMinuten += dauer * (rezeptionBelegt.Count + schwesterBelegt.Count + arztBelegt.Count);
                belegteRaumMinuten += dauer * (schwesterzimmerBelegt.Count + arztzimmerBelegt.Count);

                VerarbeiteBoxplotEvent(
                    traceEvent,
                    rezeptionQueue,
                    schwesterQueue,
                    arztQueue,
                    rezeptionBelegt,
                    schwesterBelegt,
                    arztBelegt,
                    schwesterzimmerBelegt,
                    arztzimmerBelegt);

                letzteZeit = zeit;
            }

            double auswertungsdauer = Math.Max(1.0, SimulationKonfiguration.SIMULATIONSDAUER);
            return new TagesTraceStatistik(
                belegtePersonalMinuten,
                belegteRaumMinuten,
                queueMinuten / auswertungsdauer);
        }

        private static void VerarbeiteBoxplotEvent(
            BoxplotTraceEvent traceEvent,
            HashSet<int> rezeptionQueue,
            HashSet<int> schwesterQueue,
            HashSet<int> arztQueue,
            HashSet<int> rezeptionBelegt,
            HashSet<int> schwesterBelegt,
            HashSet<int> arztBelegt,
            HashSet<int> schwesterzimmerBelegt,
            HashSet<int> arztzimmerBelegt)
        {
            int patientId = traceEvent.PatientId;
            switch (traceEvent.EventTyp)
            {
                case "betritt_rezeption_warteschlange":
                    rezeptionQueue.Add(patientId);
                    break;
                case "betritt_rezeption":
                    rezeptionQueue.Remove(patientId);
                    rezeptionBelegt.Add(patientId);
                    break;
                case "beendet_rezeption":
                case "bricht_ab_wegen_feierabend_rezeption":
                    rezeptionQueue.Remove(patientId);
                    rezeptionBelegt.Remove(patientId);
                    break;
                case "betritt_wartezimmer":
                case "betritt_schwester_warteschlange":
                    schwesterQueue.Add(patientId);
                    break;
                case "verlaesst_wartezimmer":
                case "verlaesst_wartezimmer_schwester":
                case "betritt_schwesterzimmer":
                    schwesterQueue.Remove(patientId);
                    schwesterzimmerBelegt.Add(patientId);
                    break;
                case "startet_schwester_prozess":
                    schwesterBelegt.Add(patientId);
                    break;
                case "beendet_schwester_prozess":
                case "bricht_ab_wegen_feierabend_schwester":
                    schwesterQueue.Remove(patientId);
                    schwesterBelegt.Remove(patientId);
                    schwesterzimmerBelegt.Remove(patientId);
                    break;
                case "betritt_wartezimmer_fuer_arzt":
                    arztQueue.Add(patientId);
                    break;
                case "verlaesst_wartezimmer_fuer_arzt":
                case "betritt_arztzimmer":
                    arztQueue.Remove(patientId);
                    arztzimmerBelegt.Add(patientId);
                    break;
                case "startet_arzt_behandlung":
                    arztBelegt.Add(patientId);
                    break;
                case "beendet_arzt_behandlung":
                case "bricht_ab_wegen_feierabend_arzt":
                    arztQueue.Remove(patientId);
                    arztBelegt.Remove(patientId);
                    arztzimmerBelegt.Remove(patientId);
                    break;
                case "geht_zum_ausgang":
                case "verlaesst_klinik":
                    rezeptionQueue.Remove(patientId);
                    schwesterQueue.Remove(patientId);
                    arztQueue.Remove(patientId);
                    rezeptionBelegt.Remove(patientId);
                    schwesterBelegt.Remove(patientId);
                    arztBelegt.Remove(patientId);
                    schwesterzimmerBelegt.Remove(patientId);
                    arztzimmerBelegt.Remove(patientId);
                    break;
            }
        }

        private static BoxplotTraceEvent? ParseBoxplotTraceEvent(string zeile, int index)
        {
            string[] teile = zeile.Split(';');
            if (teile.Length < 5)
                return null;

            if (!double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double zeit))
                return null;

            if (!int.TryParse(teile[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int patientId))
                return null;

            int tagIndex = Math.Max(0, (patientId - 1) / 10_000);
            return new BoxplotTraceEvent(index, tagIndex, zeit, teile[1], patientId);
        }

        private sealed record RessourcenSzenario(
            string Name,
            int Rezeptionisten,
            int Schwestern,
            int Aerzte,
            int Arztzimmer,
            int Schwesterzimmer);

        private sealed record SzenarioBoxplotDaten(
            string Name,
            IReadOnlyList<double> Wartezeiten,
            IReadOnlyList<double> PersonalAuslastungenProzent,
            IReadOnlyList<double> RaumAuslastungenProzent,
            IReadOnlyList<double> KostenProTag,
            KostenKomponenten Kosten,
            IReadOnlyList<double> Warteschlangenlaengen);

        private sealed record KostenKomponenten(double Personal, double Fix, double Material)
        {
            public double Gesamt => Personal + Fix + Material;
        }

        private sealed record BoxplotGruppe(
            string Name,
            IReadOnlyList<double> ErstesSzenario,
            IReadOnlyList<double> ZweitesSzenario);

        private sealed record BoxplotStatistik(double Min, double Q1, double Median, double Q3, double Max);

        private sealed record BoxplotSkala(bool IstKomprimiert, double Start, double Ende, double AngezeigteSpanne);

        private sealed record BoxplotTraceEvent(int Index, int TagIndex, double Zeit, string EventTyp, int PatientId);

        private sealed record TagesTraceStatistik(
            double BelegtePersonalMinuten,
            double BelegteRaumMinuten,
            double DurchschnittlicheWarteschlangenlaenge);

        private sealed record TagesBoxplotStatistik(
            double PersonalAuslastungProzent,
            double RaumAuslastungProzent,
            double Gesamtkosten,
            double Personalkosten,
            double Fixkosten,
            double Materialkosten,
            double DurchschnittlicheWarteschlangenlaenge);
    }
}
