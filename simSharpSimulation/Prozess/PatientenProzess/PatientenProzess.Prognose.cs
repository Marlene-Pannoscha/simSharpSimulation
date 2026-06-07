using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;

namespace simSharpSimulation
{
    // Diese Datei bündelt die Prognose-Logik:
    // Checkpoints, Restzeit-Schätzung und prognosebasierter Abbruch.
    internal sealed partial class PatientenProzess
    {
        // Ein Checkpoint speichert eine Prognose für den aktuellen Patientenstatus.
        // Rückgabewert:
        // - true  => der Patient wird laut Prognose noch vor Schichtende fertig
        // - false => der restliche Ablauf ist aus aktueller Sicht zu lang
        private bool ErfassePrognoseCheckpoint(Simulation env, int patientId, string phase, double prognoseRestMinuten)
        {
            double zeitpunktMinuten = (env.Now - env.StartDate).TotalMinutes;
            // Die Schichtgrenze wird hier hart gegen den prognostizierten Rest geprüft.
            // Damit kann der Hauptprozess sofort entscheiden, ob der Patient weiterlaufen darf.
            bool fertigBisSchichtende = zeitpunktMinuten + prognoseRestMinuten <= SimulationKonfiguration.SIMULATIONSDAUER;
            daten.ErfassePrognosePruefung(
                patientId,
                phase,
                zeitpunktMinuten,
                Math.Max(0.0, prognoseRestMinuten),
                fertigBisSchichtende);
            AktualisiereAktivePatientenPrognose(patientId, zeitpunktMinuten, prognoseRestMinuten);
            return fertigBisSchichtende;
        }

        // Wenn die Prognose negativ ist, verlässt der Patient die Klinik über einen normalen Ausgangspfad.
        // Wir loggen bewusst keinen separaten Prognose-Event im Trace, sondern nur die Zählung
        // in SimulationsDaten und den üblichen Weg zum Ausgang.
        private IEnumerable<Event> BrichWegenPrognoseAb(Simulation env, int patientId, string phase, TimeSpan wegZumAusgang)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.ErfassePrognoseAbbruch(env.StartDate, nowMinutes, phase);
            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(wegZumAusgang);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            EntferneAktivePatientenPrognose(patientId);
        }

        // Eine Stunde vor Schliessung wird die verbleibende Aufnahmekapazitaet geschaetzt.
        // Danach duerfen nur noch so viele neu ankommende Patienten in den Ablauf starten.
        private IEnumerable<Event> AktiviereAufnahmeprognoseEineStundeVorSchliessung(Simulation env)
        {
            double pruefzeitpunkt = BerechneAufnahmeprognoseZeitpunkt();
            double wartezeitMinuten = pruefzeitpunkt - (env.Now - env.StartDate).TotalMinutes;
            if (wartezeitMinuten > 0.0)
            {
                yield return env.Timeout(TimeSpan.FromMinutes(wartezeitMinuten));
            }

            AktiviereAufnahmeprognose(env);
        }

        private bool DarfPatientNachAufnahmeprognoseNochRein(Simulation env, int patientId)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            if (nowMinutes < BerechneAufnahmeprognoseZeitpunkt())
            {
                return true;
            }

            AktiviereAufnahmeprognose(env);
            if (aufnahmeprognoseAbgewiesenePatienten.Contains(patientId))
            {
                return false;
            }

            if (aufnahmeprognoseZugelassenePatienten.Contains(patientId))
            {
                return true;
            }

            return false;
        }

        private IEnumerable<Event> WeiseWegenAufnahmeprognoseAb(Simulation env, int patientId, TimeSpan wegZumAusgang)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.ErfassePrognoseAufnahmeAbgewiesen(env.StartDate, nowMinutes, patientId);
            daten.LogEvent(nowMinutes, "abgewiesen_wegen_aufnahmeprognose", patientId);
            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(wegZumAusgang);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            EntferneAktivePatientenPrognose(patientId);
        }

        private void AktiviereAufnahmeprognose(Simulation env)
        {
            if (restAufnahmeplaetzeEineStundeVorSchliessung.HasValue)
            {
                return;
            }

            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            double restMinuten = Math.Max(0.0, SimulationKonfiguration.SIMULATIONSDAUER - nowMinutes);
            double mittlereArztBehandlungsdauer = Math.Max(0.1, ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER);
            int kapazitaet = (int)Math.Floor(
                (restMinuten * ArztKonfiguration.ANZAHL_AERZTE) / mittlereArztBehandlungsdauer);
            int aufnahmeKapazitaet = Math.Max(0, kapazitaet);
            var aktivePatienten = aktivePatientenPrognosen.Values
                .OrderBy(p => p.PrognoseRestMinuten)
                .ThenBy(p => p.ZeitpunktMinuten)
                .ThenBy(p => p.PatientId)
                .ToList();

            foreach (var patient in aktivePatienten.Take(aufnahmeKapazitaet))
            {
                aufnahmeprognoseZugelassenePatienten.Add(patient.PatientId);
            }

            foreach (var patient in aktivePatienten.Skip(aufnahmeKapazitaet))
            {
                aufnahmeprognoseAbgewiesenePatienten.Add(patient.PatientId);
            }

            restAufnahmeplaetzeEineStundeVorSchliessung = Math.Max(0, aufnahmeKapazitaet - aktivePatienten.Count);
            daten.ErfassePrognoseAufnahmepruefung(
                env.StartDate,
                nowMinutes,
                aufnahmeKapazitaet);
        }

        private static double BerechneAufnahmeprognoseZeitpunkt()
        {
            return Math.Max(
                0.0,
                SimulationKonfiguration.SIMULATIONSDAUER -
                SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN);
        }

        private bool IstDurchAufnahmeprognoseAbgewiesen(Simulation env, int patientId)
        {
            if ((env.Now - env.StartDate).TotalMinutes >= BerechneAufnahmeprognoseZeitpunkt())
            {
                AktiviereAufnahmeprognose(env);
            }

            return aufnahmeprognoseAbgewiesenePatienten.Contains(patientId);
        }

        private void AktualisiereAktivePatientenPrognose(int patientId, double zeitpunktMinuten, double prognoseRestMinuten)
        {
            aktivePatientenPrognosen[patientId] = new AktiverPatientPrognose(
                patientId,
                zeitpunktMinuten,
                Math.Max(0.0, prognoseRestMinuten));
        }

        private void EntferneAktivePatientenPrognose(int patientId)
        {
            aktivePatientenPrognosen.Remove(patientId);
            aufnahmeprognoseZugelassenePatienten.Remove(patientId);
            aufnahmeprognoseAbgewiesenePatienten.Remove(patientId);
        }

        // Restzeit-Schaetzung direkt nach der Rezeption.
        // Falls keine Schwester-Vorbereitung mehr noetig ist, ist dieser Teil 0.
        // Falls Vorbereitung noetig ist, schaetzen wir:
        // - ggf. Bewegung und Warten bis zur Schwester
        // - Schwesterbehandlung mit Mittelwert
        private static double BerechneSchwesterRestzeitNachRezeption(
            bool brauchtVorbereitung,
            bool direktZurSchwester,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            double schwesterBehandlungsdauer,
            double schwesterWartezimmerdauer)
        {
            if (!brauchtVorbereitung)
            {
                return 0.0;
            }

            double restzeit = interneBewegungsdauer.TotalMinutes + schwesterBehandlungsdauer;
            if (!direktZurSchwester)
            {
                restzeit += interneBewegungsdauer.TotalMinutes + schwesterWartezimmerdauer;
            }

            return restzeit;
        }

        // Erwartete Schwester-Restzeit zu einem frühen Zeitpunkt, an dem noch nicht feststeht,
        // ob die Schwester-Vorbereitung wirklich anfällt.
        // Deshalb rechnen wir mit:
        // Vorbereitungswahrscheinlichkeit * erwartete Restzeit im Schwesterpfad.
        private static double BerechneErwarteteSchwesterRestzeit(
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            double vorbereitungsWahrscheinlichkeit,
            double schwesterBehandlungsdauer,
            double schwesterWartezimmerdauer)
        {
            double restzeitMitVorbereitung =
                (2.0 * interneBewegungsdauer.TotalMinutes) +
                schwesterWartezimmerdauer +
                schwesterBehandlungsdauer;

            return vorbereitungsWahrscheinlichkeit * restzeitMitVorbereitung;
        }

        // Restzeit ab abgeschlossenem Schwesterpfad:
        // Bewegung zum Arzt-Wartezimmer + erwartete Wartezimmerzeit + Arztbehandlung.
        private static double BerechneRestzeitAbSchwester(
            TimeSpan interneBewegungsdauer,
            double arztBehandlungsdauer,
            double arztWartezimmerdauer)
        {
            return (2.0 * interneBewegungsdauer.TotalMinutes) +
                   arztWartezimmerdauer +
                   arztBehandlungsdauer;
        }

        // Erwartete Restzeit nach dem Arzt, solange noch nicht entschieden ist,
        // ob der Patient zur Rezeption zurückgeht oder direkt die Klinik verlässt.
        // Der Wert ist also ein gewichteter Erwartungswert über beide Pfade.
        private static double BerechneErwarteteRestzeitNachArzt(
            TimeSpan interneBewegungsdauer,
            TimeSpan rezeptionZumAusgangDauer,
            TimeSpan arztZumAusgangDauer,
            double rezeptionsdauerNachArzt)
        {
            double restMitRezeption =
                interneBewegungsdauer.TotalMinutes +
                rezeptionsdauerNachArzt +
                rezeptionZumAusgangDauer.TotalMinutes;

            double restOhneRezeption = arztZumAusgangDauer.TotalMinutes;
            return WahrscheinlichkeitNachArztZurRezeption * restMitRezeption +
                   ((1.0 - WahrscheinlichkeitNachArztZurRezeption) * restOhneRezeption);
        }

        // Restzeit nach dem Arzt, wenn der konkrete Pfad bereits feststeht.
        // Dann brauchen wir keinen Erwartungswert mehr, sondern die direkte Pfadlänge.
        private static double BerechneRestzeitNachArztMitKonkretemPfad(
            bool gehtNachArztZurRezeption,
            TimeSpan interneBewegungsdauer,
            TimeSpan rezeptionZumAusgangDauer,
            TimeSpan arztZumAusgangDauer,
            double rezeptionsdauerNachArzt)
        {
            if (!gehtNachArztZurRezeption)
            {
                return arztZumAusgangDauer.TotalMinutes;
            }

            return interneBewegungsdauer.TotalMinutes +
                   rezeptionsdauerNachArzt +
                   rezeptionZumAusgangDauer.TotalMinutes;
        }

        // Für die Prognose wird die Wartezimmerzeit nicht aus der echten Queue geschätzt,
        // sondern aus Mittelwert * Terminfaktor.
        // Terminpatienten und Patienten ohne Termin bekommen dadurch unterschiedliche Erwartungen.
        private static double BerechneMittlereSchwesterWartezimmerzeit(bool hatTermin)
        {
            double faktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER;
            return PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * faktor;
        }

        // Auch beim Arzt verwenden wir aktuell einen gemittelten Erwartungswert.
        // Der Terminanteil mischt die beiden Wartezimmerfaktoren zu einem Gesamtwert.
        // Das ist einfach und stabil, aber noch nicht queue-genau.
        private static double BerechneMittlereArztWartezimmerzeit()
        {
            double terminAnteil = PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;
            double faktor =
                (terminAnteil * PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT) +
                ((1.0 - terminAnteil) * PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT);
            return PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT * faktor;
        }
    }
}
