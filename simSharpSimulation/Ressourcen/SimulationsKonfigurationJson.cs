using System.Collections.Generic;

namespace simSharpSimulation
{
    internal sealed class ArztKonfigurationJson
    {
        public int AnzahlAerzte { get; set; }
        public double MittlereBehandlungsdauer { get; set; }
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class SchwesterKonfigurationJson
    {
        public int AnzahlSchwestern { get; set; }
        public double MittlereBehandlungsdauer { get; set; }
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class RezeptionKonfigurationJson
    {
        public int AnzahlRezeptionisten { get; set; }
        public double MittelRezeptionszeit { get; set; }
        public double VariationskoeffizientRezeption { get; set; } = 1.0;
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class PatientenKonfigurationJson
    {
        public double ZwischenankunftErste2StundenMinuten { get; set; }
        public double ZwischenankunftNaechste3StundenMinuten { get; set; }
        public double ZwischenankunftLetzte3StundenMinuten { get; set; }
        public double TerminWahrscheinlichkeit { get; set; }
        public double TerminVorbereitungWahrscheinlichkeit { get; set; }
        public double OhneTerminVorbereitungWahrscheinlichkeit { get; set; }
        public double MittlereWartezimmerDauerSchwester { get; set; }
        public double MittlereWartezimmerDauerArzt { get; set; }
        public double MitTerminWartezimmerFaktorSchwester { get; set; }
        public double OhneTerminWartezimmerFaktorSchwester { get; set; }
        public double MitTerminWartezimmerFaktorArzt { get; set; }
        public double OhneTerminWartezimmerFaktorArzt { get; set; }
        public List<PatientenTypKonfigurationJson> TypenVerteilung { get; set; } = new();
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class PatientenTypKonfigurationJson
    {
        public PatientenTyp Typ { get; set; }
        public double Wahrscheinlichkeit { get; set; }
        public double BehandlungszeitArzt { get; set; }
        public double VariationskoeffizientArzt { get; set; }
        public double BehandlungszeitSchwester { get; set; }
        public double VariationskoeffizientSchwester { get; set; }
    }

    internal sealed class SimulationKonfigurationJson
    {
        public int RandomSeed { get; set; }
        public double Simulationsdauer { get; set; }
        public double PrognosePruefungVorSchliessungMinuten { get; set; } = 60.0;
        public double NachlaufMinuten { get; set; } = 180.0;
        public double WahrscheinlichkeitNachArztZurRezeption { get; set; } = 0.6;
        public int BewegungszeitEingangZurRezeptionSekunden { get; set; }
        public int BewegungszeitInnerhalbKlinikSekunden { get; set; }
        public int BewegungszeitArztZumAusgangSekunden { get; set; }
        public int BewegungszeitRezeptionZumAusgangSekunden { get; set; }
    }

}
