using System.Text.Json;

namespace simSharpSimulation;

public static class ArztKonfiguration
{
    private static readonly ArztKonfigurationJson Konfiguration = LadeKonfiguration();

    public static int ANZAHL_AERZTE => Konfiguration.Anzahl;

    private static ArztKonfigurationJson LadeKonfiguration()
    {
        string jsonString = KonfigurationJsonExport.Arzt;
        var config = JsonSerializer.Deserialize<ArztKonfigurationJson>(jsonString);
        return config ?? throw new InvalidOperationException("Arzt-Konfiguration konnte nicht geladen werden.");
    }
}

internal sealed class ArztKonfigurationJson
{
    public int Anzahl { get; set; }
}
