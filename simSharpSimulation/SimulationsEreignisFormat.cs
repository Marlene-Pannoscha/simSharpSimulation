using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace simSharpSimulation
{
    internal static class SimulationsEreignisFormat
    {
        private const string ZustandUnveraendert = "UNVERAENDERT";
        private static readonly Dictionary<string, (string Von, string Zu)> EventZustandsMapping =
            ErstelleEventZustandsMapping();

        internal static (string Trace, SimulationLiveEvent LiveEvent) Erzeuge(
            double zeit, string eventTyp, int patientId, int? arztId, int? schwesterId,
            double bewegungsdauerMinuten)
        {
            var (vonZustand, zuZustand) = ErmittleZustandswechsel(eventTyp);
            string zeitText = zeit.ToString("000.00", CultureInfo.InvariantCulture);
            string arztIdText = arztId.HasValue ? arztId.Value.ToString() : "";
            string schwesterIdText = schwesterId.HasValue ? schwesterId.Value.ToString() : "";
            string traceEintrag = $"{zeitText};{eventTyp};{vonZustand};{zuZustand};{patientId};{arztIdText};{schwesterIdText}";
            var liveEvent = new SimulationLiveEvent(zeit, eventTyp, patientId, vonZustand, zuZustand,
                arztId, schwesterId, bewegungsdauerMinuten);
            return (traceEintrag, liveEvent);
        }

        private static (string Von, string Zu) ErmittleZustandswechsel(string eventTyp)
        {
            if (EventZustandsMapping.TryGetValue(eventTyp, out var mapping))
            {
                return mapping;
            }

            return (ZustandUnveraendert, ZustandUnveraendert);
        }

        private static Dictionary<string, (string Von, string Zu)> ErstelleEventZustandsMapping()
        {
            string dateiPfad = ErmittleRessourcenDateiPfad("event-zustandsmapping.json");

            string json = File.ReadAllText(dateiPfad);
            var jsonDaten = JsonSerializer.Deserialize<Dictionary<string, EventZustandsEintrag>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (jsonDaten is null || jsonDaten.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Die Event-Mapping-Datei ist leer oder ungültig: {dateiPfad}");
            }

            return jsonDaten.ToDictionary(
                eintrag => eintrag.Key,
                eintrag => (eintrag.Value.Von, eintrag.Value.Zu),
                StringComparer.Ordinal);
        }

        private static string ErmittleRessourcenDateiPfad(string dateiname)
        {
            string[] kandidaten =
            {
                Path.Combine(AppContext.BaseDirectory, "Ressourcen", dateiname),
                Path.Combine(Directory.GetCurrentDirectory(), "Ressourcen", dateiname),
                Path.Combine(Directory.GetCurrentDirectory(), "simSharpSimulation", "Ressourcen", dateiname)
            };

            foreach (string kandidat in kandidaten)
            {
                if (File.Exists(kandidat))
                    return kandidat;
            }

            string? projektOrdner = ProjektPfade.FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj")
                ?? ProjektPfade.FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj");

            if (!string.IsNullOrEmpty(projektOrdner))
            {
                string pfadImProjekt = Path.Combine(projektOrdner, "Ressourcen", dateiname);
                if (File.Exists(pfadImProjekt))
                    return pfadImProjekt;
            }

            throw new FileNotFoundException(
                $"Die Ressourcen-Datei wurde nicht gefunden: {dateiname}",
                dateiname);
        }

        private sealed class EventZustandsEintrag
        {
            public string Von { get; set; } = string.Empty;
            public string Zu { get; set; } = string.Empty;
        }
    }
}
