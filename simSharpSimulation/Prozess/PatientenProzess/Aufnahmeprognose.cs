using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;
using static simSharpSimulation.SimulationsHelfer;

namespace simSharpSimulation
{
    internal sealed class Aufnahmeprognose(SimulationsDaten daten)
    {
        private bool aktiviert;
        private readonly Dictionary<int, AktiverPatientPrognose> aktivePatientenPrognosen = new();
        private readonly HashSet<int> aufnahmeprognoseZugelassenePatienten = new();
        private readonly HashSet<int> aufnahmeprognoseAbgewiesenePatienten = new();

        // Vor Schliessung wird die verbleibende Aufnahmekapazitaet geschaetzt.
        // Die Entscheidung betrifft die zu diesem Zeitpunkt aktiven Patienten.
        internal IEnumerable<Event> PlaneAufnahmeprognose(Simulation env)
        {
            double pruefzeitpunkt = Aufnahmestopp.BerechneZeitpunkt();
            double wartezeitMinuten = pruefzeitpunkt - AktuelleSimulationszeitMinuten(env);
            if (wartezeitMinuten > 0.0)
            {
                yield return env.Timeout(TimeSpan.FromMinutes(wartezeitMinuten));
            }

            AktiviereAufnahmeprognose(env);
        }

        internal bool DarfPatientNochRein(Simulation env, int patientId)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            if (jetztMinuten < Aufnahmestopp.BerechneZeitpunkt())
            {
                return true;
            }

            AktiviereAufnahmeprognose(env);
            return aufnahmeprognoseZugelassenePatienten.Contains(patientId);
        }

        private void AktiviereAufnahmeprognose(Simulation env)
        {
            // Die Aufnahmeentscheidung wird einmal pro Tag eingefroren, auch wenn mehrere Prozesse sie ausloesen.
            if (aktiviert)
            {
                return;
            }

            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            double restMinuten = Math.Max(0.0, SimulationKonfiguration.SIMULATIONSDAUER - jetztMinuten);
            int aufnahmeKapazitaet = Aufnahmestopp.BerechneArztkapazitaet(restMinuten);
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

            aktiviert = true;
            daten.ErfassePrognoseAufnahmepruefung(
                env.StartDate,
                jetztMinuten,
                aufnahmeKapazitaet);
        }

        internal bool IstAbgewiesen(Simulation env, int patientId)
        {
            if (AktuelleSimulationszeitMinuten(env) >= Aufnahmestopp.BerechneZeitpunkt())
            {
                AktiviereAufnahmeprognose(env);
            }

            return aufnahmeprognoseAbgewiesenePatienten.Contains(patientId);
        }

        internal void Aktualisiere(int patientId, double zeitpunktMinuten, double prognoseRestMinuten)
        {
            aktivePatientenPrognosen[patientId] = new AktiverPatientPrognose(
                patientId,
                zeitpunktMinuten,
                Math.Max(0.0, prognoseRestMinuten));
        }

        internal void EntfernePatient(int patientId)
        {
            aktivePatientenPrognosen.Remove(patientId);
            aufnahmeprognoseZugelassenePatienten.Remove(patientId);
            aufnahmeprognoseAbgewiesenePatienten.Remove(patientId);
        }

        private sealed record AktiverPatientPrognose(
            int PatientId,
            double ZeitpunktMinuten,
            double PrognoseRestMinuten);
    }
}
