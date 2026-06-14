using System;
using System.Collections.Generic;
using System.Globalization;

namespace simSharpSimulation;

internal static class SimulationsTraceAuswertung
{
    public static (List<double> MitTermin, List<double> OhneTermin) BerechneGesamtprozesszeitenNachTermin(
        IReadOnlyList<string> traceData)
    {
        Dictionary<int, TracePatientProzess> patienten = new();

        foreach (string zeile in traceData)
        {
            string[] teile = zeile.Split(';');
            if (teile.Length < 5)
                continue;

            if (!double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double zeit))
                continue;

            if (!int.TryParse(teile[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int patientId))
                continue;

            TracePatientProzess patient = patienten.TryGetValue(patientId, out TracePatientProzess? vorhandenerPatient)
                ? vorhandenerPatient
                : new TracePatientProzess();
            patienten[patientId] = patient;

            switch (teile[1])
            {
                case "betritt_klinik":
                    patient.Startzeit ??= zeit;
                    break;
                case "hat_termin":
                case "rezeption_hat_termin":
                    patient.HatTermin = true;
                    break;
                case "hat_keinen_termin":
                case "rezeption_ohne_termin":
                    patient.HatTermin = false;
                    break;
                case "verlaesst_klinik":
                    patient.Endzeit = zeit;
                    break;
            }
        }

        List<double> mitTermin = new();
        List<double> ohneTermin = new();

        foreach (TracePatientProzess patient in patienten.Values)
        {
            if (!patient.Startzeit.HasValue || !patient.Endzeit.HasValue || !patient.HatTermin.HasValue)
                continue;

            double dauer = Math.Max(0.0, patient.Endzeit.Value - patient.Startzeit.Value);
            if (patient.HatTermin.Value)
                mitTermin.Add(dauer);
            else
                ohneTermin.Add(dauer);
        }

        return (mitTermin, ohneTermin);
    }

    private sealed class TracePatientProzess
    {
        public double? Startzeit { get; set; }
        public double? Endzeit { get; set; }
        public bool? HatTermin { get; set; }
    }
}
