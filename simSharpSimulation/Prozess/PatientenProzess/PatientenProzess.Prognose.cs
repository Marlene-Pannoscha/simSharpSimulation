using System;
using System.Collections.Generic;
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
        }

        // Restzeit-Schätzung direkt nach der Rezeption.
        // Falls keine Schwester-Vorbereitung mehr nötig ist, ist dieser Teil 0.
        // Falls Vorbereitung nötig ist, schätzen wir:
        // - ggf. Bewegung und Warten bis zur Schwester
        // - Schwesterbehandlung mit Mittelwert
        private static double BerechneSchwesterRestzeitNachRezeption(
            bool brauchtVorbereitung,
            bool direktZurSchwester,
            bool hatTermin,
            TimeSpan interneBewegungsdauer)
        {
            if (!brauchtVorbereitung)
            {
                return 0.0;
            }

            double restzeit = interneBewegungsdauer.TotalMinutes + SchwesterKonfiguration.MITTLERE_BEHANDLUNGSDAUER;
            if (!direktZurSchwester)
            {
                restzeit += interneBewegungsdauer.TotalMinutes + BerechneMittlereSchwesterWartezimmerzeit(hatTermin);
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
            double vorbereitungsWahrscheinlichkeit)
        {
            double restzeitMitVorbereitung =
                (2.0 * interneBewegungsdauer.TotalMinutes) +
                BerechneMittlereSchwesterWartezimmerzeit(hatTermin) +
                SchwesterKonfiguration.MITTLERE_BEHANDLUNGSDAUER;

            return vorbereitungsWahrscheinlichkeit * restzeitMitVorbereitung;
        }

        // Restzeit ab abgeschlossenem Schwesterpfad:
        // Bewegung zum Arzt-Wartezimmer + erwartete Wartezimmerzeit + Arztbehandlung.
        private static double BerechneRestzeitAbSchwester(TimeSpan interneBewegungsdauer)
        {
            return (2.0 * interneBewegungsdauer.TotalMinutes) +
                   BerechneMittlereArztWartezimmerzeit() +
                   ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER;
        }

        // Erwartete Restzeit nach dem Arzt, solange noch nicht entschieden ist,
        // ob der Patient zur Rezeption zurückgeht oder direkt die Klinik verlässt.
        // Der Wert ist also ein gewichteter Erwartungswert über beide Pfade.
        private static double BerechneErwarteteRestzeitNachArzt(
            TimeSpan interneBewegungsdauer,
            TimeSpan rezeptionZumAusgangDauer,
            TimeSpan arztZumAusgangDauer)
        {
            double restMitRezeption =
                interneBewegungsdauer.TotalMinutes +
                RezeptionKonfiguration.MITTELREZEPTIONSZEIT +
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
            TimeSpan arztZumAusgangDauer)
        {
            if (!gehtNachArztZurRezeption)
            {
                return arztZumAusgangDauer.TotalMinutes;
            }

            return interneBewegungsdauer.TotalMinutes +
                   RezeptionKonfiguration.MITTELREZEPTIONSZEIT +
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
