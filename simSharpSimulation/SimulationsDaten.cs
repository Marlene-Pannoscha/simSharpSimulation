using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace simSharpSimulation
{
    /// <summary>
    /// Hält alle während der Simulation gesammelten Daten (Trace, Wartezeiten, Ankünfte).
    /// Diese Klasse entkoppelt Datenspeicherung von der eigentlichen Simulationslogik.
    /// </summary>
    public sealed class SimulationsDaten
    {
        public event Action<SimulationLiveEvent>? LiveEventOccurred;
        // Rohdaten der Simulation
        public List<string> TraceData { get; } = new();
        public List<double> Wartezeiten { get; } = new();
        public List<double> WartezeitenMitTermin { get; } = new();
        public List<double> WartezeitenOhneTermin { get; } = new();
        public Dictionary<PatientenTyp, List<double>> WartezeitenArztNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());

        public List<double> SchwesternWartezeiten { get; } = new();
        public Dictionary<PatientenTyp, List<double>> WartezeitenSchwesterNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        public List<double> SchwesternWartezeitenMitTermin { get; } = new();
        public List<double> SchwesternWartezeitenOhneTermin { get; } = new();
        public List<double> SchwesternBehandlungszeitenMitTermin { get; } = new();
        public List<double> SchwesternBehandlungszeitenOhneTermin { get; } = new();

        public List<double> RezeptionsWartezeiten { get; } = new();
        public List<double> RezeptionsWartezeitenMitTermin { get; } = new();
        public List<double> RezeptionsWartezeitenOhneTermin { get; } = new();
        public List<double> RezeptionsBehandlungszeiten { get; } = new();
        public List<double> RezeptionsBehandlungszeitenMitTermin { get; } = new();
        public List<double> RezeptionsBehandlungszeitenOhneTermin { get; } = new();

        public List<double> Gesamtprozesszeiten { get; } = new();
        public List<double> GesamtprozesszeitenMitTermin { get; } = new();
        public List<double> GesamtprozesszeitenOhneTermin { get; } = new();
        public List<double> EchteAnkunftszeiten { get; } = new();

        public Dictionary<PatientenTyp, int> PatientenTypZaehler { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => 0);

        public List<double> ArztBehandlungszeitenMitTermin { get; } = new();
        public List<double> ArztBehandlungszeitenOhneTermin { get; } = new();
        public Dictionary<PatientenTyp, List<double>> ArztBehandlungszeitenNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        public Dictionary<PatientenTyp, List<double>> SchwesternBehandlungszeitenNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        private readonly SortedDictionary<DateTime, TagesHitMissZaehler> hitMissProTag = new();

        private readonly PrognoseStatistik prognose = new();

        public int AnzahlBehandeltHit { get; private set; }
        public int AnzahlAbgebrochenMiss { get; private set; }

        public int AnzahlNichtBehandeltArztFeierabend { get; private set; }
        public int AnzahlNichtBehandeltArztGesamt => AnzahlNichtBehandeltArztFeierabend;
        public int AnzahlNichtBehandeltSchwesterFeierabend { get; private set; }
        public int AnzahlNichtBehandeltSchwesterGesamt => AnzahlNichtBehandeltSchwesterFeierabend;
        public int AnzahlNichtBehandeltRezeptionFeierabend { get; private set; }
        public int AnzahlNichtBehandeltRezeptionGesamt => AnzahlNichtBehandeltRezeptionFeierabend;

        public int AnzahlPrognosePruefungen => prognose.AnzahlPrognosePruefungen;
        public int AnzahlPrognoseRichtig => prognose.AnzahlPrognoseRichtig;
        public int AnzahlPrognoseAbbruch => prognose.AnzahlPrognoseAbbruch;
        public int AnzahlPrognoseAufnahmeAbgewiesen => prognose.AnzahlPrognoseAufnahmeAbgewiesen;

        // Abgeleitete Kennzahlen
        public double DurchschnittlicheWartezeitArzt => MittelwertOder0(Wartezeiten);
        public double DurchschnittlicheWartezeitArztMitTermin => MittelwertOder0(WartezeitenMitTermin);
        public double DurchschnittlicheWartezeitArztOhneTermin => MittelwertOder0(WartezeitenOhneTermin);
        public double DurchschnittlicheWartezeitSchwester => MittelwertOder0(SchwesternWartezeiten);
        public double DurchschnittlicheWartezeitSchwesterMitTermin => MittelwertOder0(SchwesternWartezeitenMitTermin);
        public double DurchschnittlicheWartezeitSchwesterOhneTermin => MittelwertOder0(SchwesternWartezeitenOhneTermin);
        public double DurchschnittlicheBehandlungszeitSchwesterMitTermin => MittelwertOder0(SchwesternBehandlungszeitenMitTermin);
        public double DurchschnittlicheBehandlungszeitSchwesterOhneTermin => MittelwertOder0(SchwesternBehandlungszeitenOhneTermin);
        public double DurchschnittlicheWartezeitRezeption => MittelwertOder0(RezeptionsWartezeiten);
        public double DurchschnittlicheWartezeitRezeptionMitTermin => MittelwertOder0(RezeptionsWartezeitenMitTermin);
        public double DurchschnittlicheWartezeitRezeptionOhneTermin => MittelwertOder0(RezeptionsWartezeitenOhneTermin);
        public double DurchschnittlicheBehandlungszeitRezeptionMitTermin => MittelwertOder0(RezeptionsBehandlungszeitenMitTermin);
        public double DurchschnittlicheBehandlungszeitRezeptionOhneTermin => MittelwertOder0(RezeptionsBehandlungszeitenOhneTermin);
        public double DurchschnittlicheBehandlungszeitArztMitTermin => MittelwertOder0(ArztBehandlungszeitenMitTermin);
        public double DurchschnittlicheBehandlungszeitArztOhneTermin => MittelwertOder0(ArztBehandlungszeitenOhneTermin);
        public double DurchschnittlicheGesamtprozesszeit => MittelwertOder0(Gesamtprozesszeiten);
        public double DurchschnittlicheGesamtprozesszeitMitTermin => MittelwertOder0(GesamtprozesszeitenMitTermin);
        public double DurchschnittlicheGesamtprozesszeitOhneTermin => MittelwertOder0(GesamtprozesszeitenOhneTermin);
        public double PrognoseTrefferquote => prognose.PrognoseTrefferquote;
        internal IReadOnlyList<TagesHitMissPunkt> HitMissProTag => hitMissProTag
            .Select(eintrag => new TagesHitMissPunkt(
                eintrag.Key.ToString("ddd dd.MM", CultureInfo.GetCultureInfo("de-DE")),
                eintrag.Value.Hit,
                eintrag.Value.Miss))
            .ToList();

        /// <summary>
        /// Speichert ein Ereignis im Trace-Format:
        /// "Zeit;EventTyp;VonZustand;ZuZustand;PatientId;ArztId;SchwesterId".
        /// </summary>
        public void LogEvent(double zeit, string eventTyp, int patientId, int? arztId = null, int? schwesterId = null,
            double bewegungsdauerMinuten = 0)
        {
            var (trace, liveEvent) = SimulationsEreignisFormat.Erzeuge(
                zeit, eventTyp, patientId, arztId, schwesterId, bewegungsdauerMinuten);
            TraceData.Add(trace);
            LiveEventOccurred?.Invoke(liveEvent);
        }

        public void ErfasseArztWartezeit(double wartezeitArzt, bool hatTermin, PatientenTyp patientenTyp)
        {
            Wartezeiten.Add(wartezeitArzt);
            WartezeitenArztNachTyp[patientenTyp].Add(wartezeitArzt);
            FuegeNachTerminHinzu(wartezeitArzt, hatTermin, WartezeitenMitTermin, WartezeitenOhneTermin);
        }

        public void ErfasseArztAbbruchWartezeit(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseArztAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltArztFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseArztBehandlungBegonnen(DateTime tag)
        {
            AnzahlBehandeltHit++;
            ErmittleOderErzeugeTagesHitMiss(tag).Hit++;
        }

        public void ErfasseSchwesterAbbruchWartezeit(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseSchwesterAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltSchwesterFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseRezeptionAbbruchWartezeit(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseRezeptionAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltRezeptionFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfassePrognoseAbbruch(DateTime tag, double zeitpunktMinuten, string phase)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
            prognose.ErfassePrognoseAbbruch(zeitpunktMinuten, phase);
        }

        public void ErfassePrognoseAufnahmepruefung(DateTime tag, double zeitpunktMinuten, int aufnahmeKapazitaet)
            => prognose.ErfassePrognoseAufnahmepruefung(tag, zeitpunktMinuten, aufnahmeKapazitaet);

        public void ErfassePrognoseAufnahmeAbgewiesen(DateTime tag, double zeitpunktMinuten, int patientId)
        {
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
            prognose.ErfassePrognoseAufnahmeAbgewiesen(tag, zeitpunktMinuten, patientId);
        }

        public void ErfasseSchwesterWartezeit(double wartezeitSchwester, PatientenTyp patientenTyp, bool hatTermin)
        {
            SchwesternWartezeiten.Add(wartezeitSchwester);
            WartezeitenSchwesterNachTyp[patientenTyp].Add(wartezeitSchwester);
            FuegeNachTerminHinzu(
                wartezeitSchwester,
                hatTermin,
                SchwesternWartezeitenMitTermin,
                SchwesternWartezeitenOhneTermin);
        }

        public void ErfasseSchwesterBehandlungszeit(double dauerSchwester, bool hatTermin, PatientenTyp patientenTyp)
        {
            SchwesternBehandlungszeitenNachTyp[patientenTyp].Add(dauerSchwester);
            FuegeNachTerminHinzu(
                dauerSchwester,
                hatTermin,
                SchwesternBehandlungszeitenMitTermin,
                SchwesternBehandlungszeitenOhneTermin);
        }

        public void ErfasseRezeptionWartezeit(double wartezeitRezeption, bool hatTermin)
        {
            RezeptionsWartezeiten.Add(wartezeitRezeption);
            FuegeNachTerminHinzu(
                wartezeitRezeption,
                hatTermin,
                RezeptionsWartezeitenMitTermin,
                RezeptionsWartezeitenOhneTermin);
        }

        public void ErfasseRezeptionBehandlungszeit(double dauerRezeption, bool hatTermin)
        {
            RezeptionsBehandlungszeiten.Add(dauerRezeption);
            FuegeNachTerminHinzu(
                dauerRezeption,
                hatTermin,
                RezeptionsBehandlungszeitenMitTermin,
                RezeptionsBehandlungszeitenOhneTermin);
        }

        public void ErfasseArztBehandlungszeit(double dauerArzt, bool hatTermin, PatientenTyp patientenTyp)
        {
            ArztBehandlungszeitenNachTyp[patientenTyp].Add(dauerArzt);
            FuegeNachTerminHinzu(
                dauerArzt,
                hatTermin,
                ArztBehandlungszeitenMitTermin,
                ArztBehandlungszeitenOhneTermin);
        }

        public void ErfasseGesamtprozesszeit(double gesamtprozesszeit, bool hatTermin)
        {
            Gesamtprozesszeiten.Add(gesamtprozesszeit);
            FuegeNachTerminHinzu(
                gesamtprozesszeit,
                hatTermin,
                GesamtprozesszeitenMitTermin,
                GesamtprozesszeitenOhneTermin);
        }

        public void ErfassePrognosePruefung(
            int patientId,
            string phase,
            double zeitpunktMinuten,
            double prognoseRestMinuten,
            double prognoseKorrekturMinuten,
            double prognoseBearbeitungsRestMinuten,
            double verbrauchteBearbeitungsMinuten,
            bool prognoseFertigBisSchichtende)
            => prognose.ErfassePrognosePruefung(patientId, phase, zeitpunktMinuten, prognoseRestMinuten,
                prognoseKorrekturMinuten, prognoseBearbeitungsRestMinuten, verbrauchteBearbeitungsMinuten,
                prognoseFertigBisSchichtende);

        public double ErmittlePrognoseRestzeitKorrektur(string phase)
            => prognose.ErmittlePrognoseRestzeitKorrektur(phase);

        public void SchliessePrognosen(int patientId, double endZeitpunktMinuten,
            double? gesampelteBearbeitungszeitGesamt = null)
            => prognose.SchliessePrognosen(patientId, endZeitpunktMinuten, gesampelteBearbeitungszeitGesamt);

        public string ErzeugePrognoseReportText() => prognose.ErzeugePrognoseReportText();
        public void SchreibePrognoseReport(string dateiPfad) => prognose.SchreibePrognoseReport(dateiPfad);
        public void SchreibePrognoseDatenJson(string dateiPfad)
            => prognose.SchreibePrognoseDatenJson(dateiPfad, AnzahlNichtBehandeltRezeptionFeierabend,
                AnzahlNichtBehandeltSchwesterFeierabend, AnzahlNichtBehandeltArztFeierabend);

        public void ErfassePatientenTyp(PatientenTyp typ)
        {
            PatientenTypZaehler[typ]++;
        }

        public double DurchschnittlicheArztWartezeitNachTyp(PatientenTyp typ)
        {
            return MittelwertOder0(WartezeitenArztNachTyp[typ]);
        }

        public double DurchschnittlicheSchwesterWartezeitNachTyp(PatientenTyp typ)
        {
            return MittelwertOder0(WartezeitenSchwesterNachTyp[typ]);
        }

        private static void FuegeNachTerminHinzu(
            double wert,
            bool hatTermin,
            List<double> werteMitTermin,
            List<double> werteOhneTermin)
        {
            if (hatTermin)
            {
                werteMitTermin.Add(wert);
                return;
            }

            werteOhneTermin.Add(wert);
        }

        private static double MittelwertOder0(List<double> werte)
        {
            return werte.Count > 0 ? werte.Average() : 0;
        }

        private TagesHitMissZaehler ErmittleOderErzeugeTagesHitMiss(DateTime tag)
        {
            DateTime datum = tag.Date;
            if (!hitMissProTag.TryGetValue(datum, out TagesHitMissZaehler? zaehler))
            {
                zaehler = new TagesHitMissZaehler();
                hitMissProTag[datum] = zaehler;
            }

            return zaehler;
        }

        private sealed class TagesHitMissZaehler
        {
            public int Hit { get; set; }
            public int Miss { get; set; }
        }

    }

    internal readonly record struct TagesHitMissPunkt(string Label, int Hit, int Miss);
}
