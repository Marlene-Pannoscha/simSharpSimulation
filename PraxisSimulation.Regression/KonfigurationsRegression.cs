using System.Text.Json.Nodes;
using simSharpSimulation;

internal static class KonfigurationsRegression
{
    internal static void Pruefe()
    {
        string quelle = Path.Combine(AppContext.BaseDirectory, "Ressourcen");
        using var temporaer = new TemporaeresVerzeichnis();
        foreach (string datei in Directory.GetFiles(quelle, "*.json"))
            File.Copy(datei, Path.Combine(temporaer.Pfad, Path.GetFileName(datei)));

        KonfigurationJsonExport.LadeAlle(temporaer.Pfad);
        int aerzte = ArztKonfiguration.ANZAHL_AERZTE;
        int seed = SimulationKonfiguration.RANDOM_SEED;
        var finanzen = KonfigurationJsonExport.Finanzen;
        string arztPfad = Path.Combine(temporaer.Pfad, "arzt-konfiguration.json");
        string simulationPfad = Path.Combine(temporaer.Pfad, "simulation-konfiguration.json");
        string patientenPfad = Path.Combine(temporaer.Pfad, "patienten-konfiguration.json");
        string finanzPfad = Path.Combine(temporaer.Pfad, "finanz-konfiguration.json");

        SchreibeWert(arztPfad, "AnzahlAerzte", aerzte + 1);
        File.WriteAllText(finanzPfad, "{");
        Erwartet<InvalidDataException>(() => KonfigurationJsonExport.LadeAlle(temporaer.Pfad), "finanz-konfiguration.json");
        PruefeUnveraendert();

        File.Delete(finanzPfad);
        Erwartet<FileNotFoundException>(() => KonfigurationJsonExport.LadeAlle(temporaer.Pfad), "finanz-konfiguration.json");
        PruefeUnveraendert();

        File.Copy(Path.Combine(quelle, "finanz-konfiguration.json"), finanzPfad);
        SchreibeWert(patientenPfad, "ZwischenankunftErste2StundenMinuten", 0);
        Erwartet<InvalidDataException>(() => KonfigurationJsonExport.LadeAlle(temporaer.Pfad), "ZwischenankunftErste2StundenMinuten");
        PruefeUnveraendert();

        File.Copy(Path.Combine(quelle, "patienten-konfiguration.json"), patientenPfad, overwrite: true);
        SchreibeWert(simulationPfad, "NachlaufMinuten", 20);
        KonfigurationJsonExport.LadeAlle(temporaer.Pfad);
        if (SimulationKonfiguration.NACHLAUF_MINUTEN != 20 || ArztKonfiguration.ANZAHL_AERZTE != aerzte + 1)
            throw new InvalidOperationException("Gueltige Konfiguration wurde nicht komplett uebernommen.");

        KonfigurationJsonExport.LadeAlle();
        if (SimulationKonfiguration.NACHLAUF_MINUTEN != 180)
            throw new InvalidOperationException("Der historische Nachlauf muss fuer alte JSON-Dateien gelten.");

        Console.WriteLine("Konfigurationsregression bestanden.");

        void PruefeUnveraendert()
        {
            if (ArztKonfiguration.ANZAHL_AERZTE != aerzte || SimulationKonfiguration.RANDOM_SEED != seed ||
                !ReferenceEquals(KonfigurationJsonExport.Finanzen, finanzen))
                throw new InvalidOperationException("Fehlgeschlagenes Laden hat die aktive Konfiguration veraendert.");
        }
    }

    private static void SchreibeWert(string datei, string feld, int wert)
    {
        var json = JsonNode.Parse(File.ReadAllText(datei))!.AsObject();
        json[feld] = wert;
        File.WriteAllText(datei, json.ToJsonString());
    }

    private static void Erwartet<T>(Action ausfuehren, string meldung) where T : Exception
    {
        try
        {
            ausfuehren();
        }
        catch (T ex) when (ex.Message.Contains(meldung, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"Erwartet: {typeof(T).Name} fuer {meldung}.");
    }

    private sealed class TemporaeresVerzeichnis : IDisposable
    {
        internal TemporaeresVerzeichnis() => Pfad = Directory.CreateTempSubdirectory("praxis-regression-").FullName;
        internal string Pfad { get; }
        public void Dispose()
        {
            foreach (string datei in Directory.GetFiles(Pfad))
                File.Delete(datei);
            Directory.Delete(Pfad);
        }
    }
}
