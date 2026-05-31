using System.Text.Json;

namespace simSharpSimulation;

public static class SchwesterKonfiguration
{
    private static readonly SchwesterKonfigurationJson Konfiguration = LadeKonfiguration();

    public static int ANZAHL_SCHWESTERN => Konfiguration.Anzahl;

    private static SchwesterKonfigurationJson LadeKonfiguration()
    {
        string jsonString = KonfigurationJsonExport.Schwester;
        var config = JsonSerializer.Deserialize<SchwesterKonfigurationJson>(jsonString);
        return config ?? throw new InvalidOperationException("Schwester-Konfiguration konnte nicht geladen werden.");
    }
}

internal sealed class SchwesterKonfigurationJson
{
    public int Anzahl { get; set; }
}
