using System;
using System.IO;

namespace simSharpSimulation
{
    internal static class Konfigurationspruefung
    {
        private const string Arzt = "arzt-konfiguration.json";
        private const string Schwester = "schwester-konfiguration.json";
        private const string Rezeption = "rezeption-konfiguration.json";
        private const string Patienten = "patienten-konfiguration.json";
        private const string Simulation = "simulation-konfiguration.json";
        private const string Finanzen = "finanz-konfiguration.json";

        internal static void Pruefe(ArztKonfigurationJson arzt, SchwesterKonfigurationJson schwester,
            RezeptionKonfigurationJson rezeption, PatientenKonfigurationJson patienten,
            SimulationKonfigurationJson simulation)
        {
            Positiv(arzt.AnzahlAerzte, Arzt, nameof(arzt.AnzahlAerzte));
            Positiv(schwester.AnzahlSchwestern, Schwester, nameof(schwester.AnzahlSchwestern));
            Positiv(rezeption.AnzahlRezeptionisten, Rezeption, nameof(rezeption.AnzahlRezeptionisten));
            Positiv(rezeption.MittelRezeptionszeit, Rezeption, nameof(rezeption.MittelRezeptionszeit));
            NichtNegativ(rezeption.VariationskoeffizientRezeption, Rezeption, nameof(rezeption.VariationskoeffizientRezeption));

            NichtNegativ(simulation.Simulationsdauer, Simulation, nameof(simulation.Simulationsdauer));
            NichtNegativ(simulation.NachlaufMinuten, Simulation, nameof(simulation.NachlaufMinuten));
            NichtNegativ(simulation.PrognosePruefungVorSchliessungMinuten, Simulation, nameof(simulation.PrognosePruefungVorSchliessungMinuten));
            Wahrscheinlichkeit(simulation.WahrscheinlichkeitNachArztZurRezeption, Simulation, nameof(simulation.WahrscheinlichkeitNachArztZurRezeption));
            NichtNegativ(simulation.BewegungszeitEingangZurRezeptionSekunden, Simulation, nameof(simulation.BewegungszeitEingangZurRezeptionSekunden));
            NichtNegativ(simulation.BewegungszeitInnerhalbKlinikSekunden, Simulation, nameof(simulation.BewegungszeitInnerhalbKlinikSekunden));
            NichtNegativ(simulation.BewegungszeitArztZumAusgangSekunden, Simulation, nameof(simulation.BewegungszeitArztZumAusgangSekunden));
            NichtNegativ(simulation.BewegungszeitRezeptionZumAusgangSekunden, Simulation, nameof(simulation.BewegungszeitRezeptionZumAusgangSekunden));
            if (simulation.Simulationsdauer + simulation.NachlaufMinuten >= TimeSpan.MaxValue.TotalMinutes)
                throw Fehler(Simulation, nameof(simulation.Simulationsdauer), "Tagesdauer mit Nachlauf ist zu gross.");

            PruefePatienten(patienten, simulation.Simulationsdauer);
        }

        private static void PruefePatienten(PatientenKonfigurationJson patienten, double dauer)
        {
            if (dauer > 0)
                Positiv(patienten.ZwischenankunftErste2StundenMinuten, Patienten, nameof(patienten.ZwischenankunftErste2StundenMinuten));
            if (dauer > PatientenKonfiguration.BeginnZweiteAnkunftsphaseMinuten)
                Positiv(patienten.ZwischenankunftNaechste3StundenMinuten, Patienten, nameof(patienten.ZwischenankunftNaechste3StundenMinuten));
            if (dauer > PatientenKonfiguration.BeginnDritteAnkunftsphaseMinuten)
                Positiv(patienten.ZwischenankunftLetzte3StundenMinuten, Patienten, nameof(patienten.ZwischenankunftLetzte3StundenMinuten));

            Wahrscheinlichkeit(patienten.TerminWahrscheinlichkeit, Patienten, nameof(patienten.TerminWahrscheinlichkeit));
            Wahrscheinlichkeit(patienten.TerminVorbereitungWahrscheinlichkeit, Patienten, nameof(patienten.TerminVorbereitungWahrscheinlichkeit));
            Wahrscheinlichkeit(patienten.OhneTerminVorbereitungWahrscheinlichkeit, Patienten, nameof(patienten.OhneTerminVorbereitungWahrscheinlichkeit));

            if (patienten.TypenVerteilung is null)
                throw Fehler(Patienten, nameof(patienten.TypenVerteilung), "Eine Liste wird erwartet.");
            foreach (var typ in patienten.TypenVerteilung)
            {
                if (typ is null || !Enum.IsDefined(typ.Typ))
                    throw Fehler(Patienten, nameof(patienten.TypenVerteilung), "Unbekannter Patiententyp.");

                string pfad = $"TypenVerteilung[{typ.Typ}]";
                Wahrscheinlichkeit(typ.Wahrscheinlichkeit, Patienten, $"{pfad}.{nameof(typ.Wahrscheinlichkeit)}");
                Positiv(typ.BehandlungszeitArzt, Patienten, $"{pfad}.{nameof(typ.BehandlungszeitArzt)}");
                Positiv(typ.BehandlungszeitSchwester, Patienten, $"{pfad}.{nameof(typ.BehandlungszeitSchwester)}");
                NichtNegativ(typ.VariationskoeffizientArzt, Patienten, $"{pfad}.{nameof(typ.VariationskoeffizientArzt)}");
                NichtNegativ(typ.VariationskoeffizientSchwester, Patienten, $"{pfad}.{nameof(typ.VariationskoeffizientSchwester)}");
            }
        }

        internal static void PruefeFinanzen(FinanzKonfigurationJson finanzen)
        {
            if (finanzen.Personal is null || finanzen.Fixkosten is null || finanzen.VariableKosten is null ||
                finanzen.Versicherung is null || finanzen.Saisonfaktoren is null)
                throw Fehler(Finanzen, "$", "Finanzabschnitte muessen Objekte sein.");

            Positiv(finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt, Finanzen, "Fixkosten.AnzahlBehandlungsraeumeArzt");
            Positiv(finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester, Finanzen, "Fixkosten.AnzahlBehandlungsraeumeSchwester");
            Wahrscheinlichkeit(finanzen.Versicherung.AnteilPrivatversichert, Finanzen, "Versicherung.AnteilPrivatversichert");
        }

        private static void Positiv(double wert, string datei, string feld)
        {
            if (!double.IsFinite(wert) || wert <= 0)
                throw Fehler(datei, feld, "Erwartet eine endliche Zahl groesser als 0.");
        }

        private static void NichtNegativ(double wert, string datei, string feld)
        {
            if (!double.IsFinite(wert) || wert < 0)
                throw Fehler(datei, feld, "Erwartet eine endliche Zahl groesser oder gleich 0.");
        }

        private static void Wahrscheinlichkeit(double wert, string datei, string feld)
        {
            if (!double.IsFinite(wert) || wert < 0 || wert > 1)
                throw Fehler(datei, feld, "Erwartet eine Wahrscheinlichkeit zwischen 0 und 1.");
        }

        private static InvalidDataException Fehler(string datei, string feld, string grund)
            => new($"{datei}, {feld}. {grund}");
    }
}
