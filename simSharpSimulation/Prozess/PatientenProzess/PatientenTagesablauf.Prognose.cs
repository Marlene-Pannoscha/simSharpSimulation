using System;
using System.Collections.Generic;
using SimSharp;
using static simSharpSimulation.SimulationsHelfer;

namespace simSharpSimulation
{
    // Diese Datei bündelt die Prognose-Logik:
    // Checkpoints, Restzeit-Schätzung und prognosebasierter Abbruch.
    internal sealed partial class PatientenTagesablauf
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
            Aufnahme.Aktualisiere(patientId, zeitpunktMinuten, kalibriertePrognoseRestMinuten);
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

        private IEnumerable<Event> WeiseWegenAufnahmeprognoseAb(Simulation env, int patientId, TimeSpan wegZumAusgang)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.ErfassePrognoseAufnahmeAbgewiesen(env.StartDate, jetztMinuten, patientId);
            daten.LogEvent(jetztMinuten, "abgewiesen_wegen_aufnahmeprognose", patientId);
            foreach (Event ev in VerlasseKlinikNachAbbruch(env, patientId, wegZumAusgang))
                yield return ev;
        }

        private void EntferneAktivePatientenPrognose(int patientId)
        {
            // Klinikabgaenge muessen aus allen Schaetzmodellen verschwinden, damit sie keine weitere Kapazitaet belegen.
            Aufnahme.EntfernePatient(patientId);
            rezeptionStatus.EntfernePatient(patientId);
            schwesterStatus.EntfernePatient(patientId);
            arztStatus.EntfernePatient(patientId);
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
