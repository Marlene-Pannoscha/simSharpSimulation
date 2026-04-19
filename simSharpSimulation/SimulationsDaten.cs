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

        public double DurchschnittlicheWartezeitArzt => Wartezeiten.Count > 0 ? Wartezeiten.Average() : 0;
        public double DurchschnittlicheWartezeitArztMitTermin => WartezeitenMitTermin.Count > 0 ? WartezeitenMitTermin.Average() : 0;
        public double DurchschnittlicheWartezeitArztOhneTermin => WartezeitenOhneTermin.Count > 0 ? WartezeitenOhneTermin.Average() : 0;
        public double DurchschnittlicheWartezeitSchwester => SchwesternWartezeiten.Count > 0 ? SchwesternWartezeiten.Average() : 0;
        public double DurchschnittlicheWartezeitSchwesterMitTermin => SchwesternWartezeitenMitTermin.Count > 0 ? SchwesternWartezeitenMitTermin.Average() : 0;
        public double DurchschnittlicheWartezeitSchwesterOhneTermin => SchwesternWartezeitenOhneTermin.Count > 0 ? SchwesternWartezeitenOhneTermin.Average() : 0;
        public double DurchschnittlicheBehandlungszeitSchwesterMitTermin => SchwesternBehandlungszeitenMitTermin.Count > 0 ? SchwesternBehandlungszeitenMitTermin.Average() : 0;
        public double DurchschnittlicheBehandlungszeitSchwesterOhneTermin => SchwesternBehandlungszeitenOhneTermin.Count > 0 ? SchwesternBehandlungszeitenOhneTermin.Average() : 0;
        public double DurchschnittlicheWartezeitRezeption => RezeptionsWartezeiten.Count > 0 ? RezeptionsWartezeiten.Average() : 0;
        public double DurchschnittlicheWartezeitRezeptionMitTermin => RezeptionsWartezeitenMitTermin.Count > 0 ? RezeptionsWartezeitenMitTermin.Average() : 0;
        public double DurchschnittlicheWartezeitRezeptionOhneTermin => RezeptionsWartezeitenOhneTermin.Count > 0 ? RezeptionsWartezeitenOhneTermin.Average() : 0;
        public double DurchschnittlicheBehandlungszeitRezeptionMitTermin => RezeptionsBehandlungszeitenMitTermin.Count > 0 ? RezeptionsBehandlungszeitenMitTermin.Average() : 0;
        public double DurchschnittlicheBehandlungszeitRezeptionOhneTermin => RezeptionsBehandlungszeitenOhneTermin.Count > 0 ? RezeptionsBehandlungszeitenOhneTermin.Average() : 0;
        public double DurchschnittlicheBehandlungszeitArztMitTermin => ArztBehandlungszeitenMitTermin.Count > 0 ? ArztBehandlungszeitenMitTermin.Average() : 0;
        public double DurchschnittlicheBehandlungszeitArztOhneTermin => ArztBehandlungszeitenOhneTermin.Count > 0 ? ArztBehandlungszeitenOhneTermin.Average() : 0;
        public double DurchschnittlicheGesamtprozesszeit => Gesamtprozesszeiten.Count > 0 ? Gesamtprozesszeiten.Average() : 0;
        public double DurchschnittlicheGesamtprozesszeitMitTermin => GesamtprozesszeitenMitTermin.Count > 0 ? GesamtprozesszeitenMitTermin.Average() : 0;
        public double DurchschnittlicheGesamtprozesszeitOhneTermin => GesamtprozesszeitenOhneTermin.Count > 0 ? GesamtprozesszeitenOhneTermin.Average() : 0;

        /// <summary>
        /// Speichert ein Ereignis im Trace-Format: "Zeit;EventTyp;PatientId;ArztId;SchwesterId".
        /// </summary>
        public void LogEvent(double zeit, string eventTyp, int patientId, int? arztId = null, int? schwesterId = null)
        {
            string timeStr = zeit.ToString("000.00", CultureInfo.InvariantCulture);
            string arztStr = arztId.HasValue ? arztId.Value.ToString() : "";
            string schwesterStr = schwesterId.HasValue ? schwesterId.Value.ToString() : "";
            string logEntry = $"{timeStr};{eventTyp};{patientId};{arztStr};{schwesterStr}";
            TraceData.Add(logEntry);
        }

        public void ErfasseArztWartezeit(double wartezeitArzt, bool hatTermin, PatientenTyp patientenTyp)
        {
            Wartezeiten.Add(wartezeitArzt);
            WartezeitenArztNachTyp[patientenTyp].Add(wartezeitArzt);
            if (hatTermin)
                WartezeitenMitTermin.Add(wartezeitArzt);
            else
                WartezeitenOhneTermin.Add(wartezeitArzt);
        }

        public void ErfasseSchwesterWartezeit(double wartezeitSchwester, PatientenTyp patientenTyp, bool hatTermin)
        {
            SchwesternWartezeiten.Add(wartezeitSchwester);
            WartezeitenSchwesterNachTyp[patientenTyp].Add(wartezeitSchwester);
            if (hatTermin)
                SchwesternWartezeitenMitTermin.Add(wartezeitSchwester);
            else
                SchwesternWartezeitenOhneTermin.Add(wartezeitSchwester);
        }

        public void ErfasseSchwesterBehandlungszeit(double dauerSchwester, bool hatTermin)
        {
            if (hatTermin)
                SchwesternBehandlungszeitenMitTermin.Add(dauerSchwester);
            else
                SchwesternBehandlungszeitenOhneTermin.Add(dauerSchwester);
        }

        public void ErfasseRezeptionWartezeit(double wartezeitRezeption, bool hatTermin)
        {
            RezeptionsWartezeiten.Add(wartezeitRezeption);
            if (hatTermin)
                RezeptionsWartezeitenMitTermin.Add(wartezeitRezeption);
            else
                RezeptionsWartezeitenOhneTermin.Add(wartezeitRezeption);
        }

        public void ErfasseRezeptionBehandlungszeit(double dauerRezeption, bool hatTermin)
        {
            if (hatTermin)
                RezeptionsBehandlungszeitenMitTermin.Add(dauerRezeption);
            else
                RezeptionsBehandlungszeitenOhneTermin.Add(dauerRezeption);
        }

        public void ErfasseArztBehandlungszeit(double dauerArzt, bool hatTermin)
        {
            if (hatTermin)
                ArztBehandlungszeitenMitTermin.Add(dauerArzt);
            else
                ArztBehandlungszeitenOhneTermin.Add(dauerArzt);
        }

        public void ErfasseGesamtprozesszeit(double gesamtprozesszeit, bool hatTermin)
        {
            Gesamtprozesszeiten.Add(gesamtprozesszeit);
            if (hatTermin)
                GesamtprozesszeitenMitTermin.Add(gesamtprozesszeit);
            else
                GesamtprozesszeitenOhneTermin.Add(gesamtprozesszeit);
        }

        public void ErfassePatientenTyp(PatientenTyp typ)
        {
            PatientenTypZaehler[typ]++;
        }

        public double DurchschnittlicheArztWartezeitNachTyp(PatientenTyp typ)
        {
            var werte = WartezeitenArztNachTyp[typ];
            return werte.Count > 0 ? werte.Average() : 0;
        }

        public double DurchschnittlicheSchwesterWartezeitNachTyp(PatientenTyp typ)
        {
            var werte = WartezeitenSchwesterNachTyp[typ];
            return werte.Count > 0 ? werte.Average() : 0;
        }
    }
}
