using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;
using static simSharpSimulation.SimulationsHelfer;

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
        private bool ErfassePrognoseCheckpoint(
            Simulation env,
            int patientId,
            string phase,
            double prognoseRestMinuten,
            double prognoseBearbeitungsRestMinuten,
            double verbrauchteBearbeitungsMinuten)
        {
            double zeitpunktMinuten = AktuelleSimulationszeitMinuten(env);
            double prognoseKorrekturMinuten = daten.ErmittlePrognoseRestzeitKorrektur(phase);
            double kalibriertePrognoseRestMinuten = Math.Max(
                0.0,
                prognoseRestMinuten + prognoseKorrekturMinuten);
            // Die Schichtgrenze wird hier hart gegen den prognostizierten Rest geprüft.
            // Damit kann der Hauptprozess sofort entscheiden, ob der Patient weiterlaufen darf.
            bool fertigBisSchichtende = zeitpunktMinuten + kalibriertePrognoseRestMinuten <= SimulationKonfiguration.SIMULATIONSDAUER;
            daten.ErfassePrognosePruefung(
                patientId,
                phase,
                zeitpunktMinuten,
                kalibriertePrognoseRestMinuten,
                prognoseKorrekturMinuten,
                Math.Max(0.0, prognoseBearbeitungsRestMinuten),
                Math.Max(0.0, verbrauchteBearbeitungsMinuten),
                fertigBisSchichtende);
            AktualisiereAktivePatientenPrognose(patientId, zeitpunktMinuten, kalibriertePrognoseRestMinuten);
            return fertigBisSchichtende;
        }

        // Wenn die Prognose negativ ist, verlässt der Patient die Klinik über einen normalen Ausgangspfad.
        // Wir loggen bewusst keinen separaten Prognose-Event im Trace, sondern nur die Zählung
        // in SimulationsDaten und den üblichen Weg zum Ausgang.
        private IEnumerable<Event> BrichWegenPrognoseAb(Simulation env, int patientId, string phase, TimeSpan wegZumAusgang)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.ErfassePrognoseAbbruch(env.StartDate, jetztMinuten, phase);
            foreach (Event ev in VerlasseKlinikNachAbbruch(env, patientId, wegZumAusgang))
                yield return ev;
        }

        // Eine Stunde vor Schliessung wird die verbleibende Aufnahmekapazitaet geschaetzt.
        // Danach duerfen nur noch so viele neu ankommende Patienten in den Ablauf starten.
        private IEnumerable<Event> AktiviereAufnahmeprognoseEineStundeVorSchliessung(Simulation env)
        {
            double pruefzeitpunkt = BerechneAufnahmeprognoseZeitpunkt();
            double wartezeitMinuten = pruefzeitpunkt - AktuelleSimulationszeitMinuten(env);
            if (wartezeitMinuten > 0.0)
            {
                yield return env.Timeout(TimeSpan.FromMinutes(wartezeitMinuten));
            }

            AktiviereAufnahmeprognose(env);
        }

        private bool DarfPatientNachAufnahmeprognoseNochRein(Simulation env, int patientId)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            if (jetztMinuten < BerechneAufnahmeprognoseZeitpunkt())
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
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.ErfassePrognoseAufnahmeAbgewiesen(env.StartDate, jetztMinuten, patientId);
            daten.LogEvent(jetztMinuten, "abgewiesen_wegen_aufnahmeprognose", patientId);
            foreach (Event ev in VerlasseKlinikNachAbbruch(env, patientId, wegZumAusgang))
                yield return ev;
        }

        private void AktiviereAufnahmeprognose(Simulation env)
        {
            // Die Aufnahmeentscheidung wird einmal pro Tag eingefroren, auch wenn mehrere Prozesse sie ausloesen.
            if (restAufnahmeplaetzeEineStundeVorSchliessung.HasValue)
            {
                return;
            }

            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            double restMinuten = Math.Max(0.0, SimulationKonfiguration.SIMULATIONSDAUER - jetztMinuten);
            double mittlereArztBehandlungsdauer = Math.Max(0.1, ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER);
            int kapazitaet = (int)Math.Floor(
                (restMinuten * ArztKonfiguration.ANZAHL_AERZTE) / mittlereArztBehandlungsdauer);
            int aufnahmeKapazitaet = Math.Max(0, kapazitaet);
            // Die verbleibende Arztkapazitaet begrenzt die Zulassung. Kuerzere Restprognosen werden bevorzugt;
            // Checkpoint-Zeitpunkt und Patienten-ID sorgen bei Gleichstand fuer eine feste Reihenfolge.
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

            // Der Wert dient auch als Aktivierungsmarker; freie Restplaetze werden hier nicht an neue Ankuenfte vergeben.
            restAufnahmeplaetzeEineStundeVorSchliessung = Math.Max(0, aufnahmeKapazitaet - aktivePatienten.Count);
            daten.ErfassePrognoseAufnahmepruefung(
                env.StartDate,
                jetztMinuten,
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
            if (AktuelleSimulationszeitMinuten(env) >= BerechneAufnahmeprognoseZeitpunkt())
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
            // Klinikabgaenge muessen aus allen Schaetzmodellen verschwinden, damit sie keine weitere Kapazitaet belegen.
            aktivePatientenPrognosen.Remove(patientId);
            aufnahmeprognoseZugelassenePatienten.Remove(patientId);
            aufnahmeprognoseAbgewiesenePatienten.Remove(patientId);
            rezeptionStatus?.EntfernePatient(patientId);
            schwesterStatus?.EntfernePatient(patientId);
            arztStatus?.EntfernePatient(patientId);
        }

        // Restzeit-Schaetzung direkt nach der Rezeption.
        // Falls keine Schwester-Vorbereitung mehr noetig ist, ist dieser Teil 0.
        // Falls Vorbereitung noetig ist, schaetzen wir:
        // - ggf. Bewegung und Warten bis zur Schwester
        // - Schwesterbehandlung mit der vorab gezogenen Dauer
        private static double BerechneSchwesterRestzeitNachRezeption(
            bool brauchtVorbereitung,
            bool direktZurSchwester,
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

        private double SchaetzeRezeptionsQueueWartezeit(
            Simulation env,
            int patientId,
            double behandlungsdauer,
            double minutenBisAnkunft = 0.0)
        {
            return SchaetzeQueueWartezeit(
                env, rezeptionStatus, patientId, behandlungsdauer, minutenBisAnkunft);
        }

        private void PlaneRezeptionsAnkunft(
            Simulation env,
            int patientId,
            double minutenBisAnkunft,
            double behandlungsdauer)
        {
            PlaneRessourcenAnkunft(
                env, rezeptionStatus, patientId, minutenBisAnkunft, behandlungsdauer);
        }

        private double SchaetzeSchwesterQueueWartezeit(
            Simulation env,
            int patientId,
            double behandlungsdauer,
            PatientenTyp patientenTyp,
            double minutenBisAnkunft = 0.0)
        {
            return SchaetzeQueueWartezeit(
                env, schwesterStatus, patientId, behandlungsdauer, minutenBisAnkunft, ErmittlePatientenPrioritaet(patientenTyp));
        }

        private void PlaneSchwesterAnkunft(
            Simulation env,
            int patientId,
            double minutenBisAnkunft,
            double behandlungsdauer,
            PatientenTyp patientenTyp)
        {
            PlaneRessourcenAnkunft(
                env, schwesterStatus, patientId, minutenBisAnkunft, behandlungsdauer, ErmittlePatientenPrioritaet(patientenTyp));
        }

        private double SchaetzeArztQueueWartezeit(
            Simulation env,
            int patientId,
            double behandlungsdauer,
            PatientenTyp patientenTyp,
            double minutenBisAnkunft = 0.0)
        {
            return SchaetzeQueueWartezeit(
                env, arztStatus, patientId, behandlungsdauer, minutenBisAnkunft, ErmittlePatientenPrioritaet(patientenTyp));
        }

        private void PlaneArztAnkunft(
            Simulation env,
            int patientId,
            double minutenBisAnkunft,
            double behandlungsdauer,
            PatientenTyp patientenTyp)
        {
            PlaneRessourcenAnkunft(
                env, arztStatus, patientId, minutenBisAnkunft, behandlungsdauer, ErmittlePatientenPrioritaet(patientenTyp));
        }

        private static double SchaetzeQueueWartezeit(
            Simulation env,
            PrognoseRessourcenStatus status,
            int patientId,
            double behandlungsdauer,
            double minutenBisAnkunft,
            int prioritaet = 0)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            return status.SchaetzeWartezeit(
                jetztMinuten,
                jetztMinuten + Math.Max(0.0, minutenBisAnkunft),
                patientId,
                behandlungsdauer,
                prioritaet);
        }

        private static void PlaneRessourcenAnkunft(
            Simulation env,
            PrognoseRessourcenStatus status,
            int patientId,
            double minutenBisAnkunft,
            double behandlungsdauer,
            int prioritaet = 0)
        {
            status.RegistriereGeplanteAnkunft(
                patientId,
                AktuelleSimulationszeitMinuten(env) + Math.Max(0.0, minutenBisAnkunft),
                behandlungsdauer,
                prioritaet);
        }
    }
}
