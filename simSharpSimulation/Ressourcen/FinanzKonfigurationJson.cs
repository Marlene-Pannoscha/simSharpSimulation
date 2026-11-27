using System.Collections.Generic;
using System.Linq;

namespace simSharpSimulation
{
    internal sealed class FinanzKonfigurationJson
    {
        public PersonalKostenJson Personal { get; set; } = new();
        public FixkostenJson Fixkosten { get; set; } = new();
        public VariableKostenJson VariableKosten { get; set; } = new();
        public VersicherungsKostenJson Versicherung { get; set; } = new();
        public SaisonfaktorenJson Saisonfaktoren { get; set; } = new();
    }

    internal sealed class PersonalKostenJson
    {
        public double ArztLohnProPatient { get; set; } = 30.0;
        public double ArztLohnProStunde { get; set; } = 85.0;
        public double SchwesterLohnProStunde { get; set; } = 32.0;
        public double RezeptionLohnProStunde { get; set; } = 24.0;
        public int ArbeitsstundenProTag { get; set; } = 8;
    }

    internal sealed class FixkostenJson
    {
        public List<MietkostenMietAufteilung> MietkostenAufteilung { get; set; } = new();
        public int AnzahlBehandlungsraeumeSchwester { get; set; }
        public double FlaecheBehandlungsraumSchwesterQuadratmeter { get; set; }
        public int AnzahlBehandlungsraeumeArzt { get; set; }
        public double FlaecheBehandlungsraumArztQuadratmeter { get; set; }
        public double FlaecheWartezimmerQuadratmeter { get; set; }
        public double InfrastrukturProTag { get; set; }
        public double ITUndVerwaltungProTag { get; set; }
        public double VersicherungenProTag { get; set; }
        public double EnergiekostenProQmProMonat { get; set; }
        public double ReinigungskostenProQmProMonat { get; set; }
        // JSON uses 'GeraeteLeasingProTag' (no hyphen/umlaut) — map to this property name.
        public double GeraeteLeasingProTag { get; set; }
        public double GeraeteWartungProTag { get; set; }
        public double SonstigeFixkostenProTag { get; set; }

        public double BerechneMietkostenProTag()
        {
            double flaecheGesamt = (AnzahlBehandlungsraeumeSchwester * FlaecheBehandlungsraumSchwesterQuadratmeter)
                + (AnzahlBehandlungsraeumeArzt * FlaecheBehandlungsraumArztQuadratmeter)
                + FlaecheWartezimmerQuadratmeter;

            // Bestimme geeigneten Preis pro Quadratmeter aus der Aufteilung.
            var aufteilung = MietkostenAufteilung?.FirstOrDefault(s => flaecheGesamt >= s.MinFlaeche && flaecheGesamt <= s.MaxFlaeche);
            double kostenProQm = aufteilung?.KostenProQm ?? MietkostenAufteilung?.LastOrDefault()?.KostenProQm ?? 0.0;
            return (kostenProQm * Math.Max(flaecheGesamt, 0.0) * 12.0) / 365.0;
        }
    }

    internal sealed class VariableKostenJson
    {
        public double MedizinischesMaterialProPatient { get; set; } = 13.85;
    }

    internal sealed class MietkostenMietAufteilung
    {
        public double MinFlaeche { get; set; }
        public double MaxFlaeche { get; set; }
        public double KostenProQm { get; set; }
    }

    internal sealed class VersicherungsKostenJson
    {
        public double AnteilPrivatversichert { get; set; } = 0.2;
        public double EinnahmePrivatpatient { get; set; } = 150.0;
        public double EinnahmeGesetzlichPatient { get; set; } = 90.0;
    }

    internal sealed class SaisonfaktorenJson
    {
        public double Winter { get; set; }
        public double Fruehling { get; set; }
        public double Sommer { get; set; }
        public double Herbst { get; set; }
    }
}
