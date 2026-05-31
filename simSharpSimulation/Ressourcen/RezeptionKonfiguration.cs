using System.Text.Json;

namespace simSharpSimulation;

public static class RezeptionKonfiguration
{
    private static readonly RezeptionKonfigurationJson Konfiguration = LadeKonfiguration();

    public static int ANZAHL_REZEPTIONISTEN => Konfiguration.Anzahl;

    private static RezeptionKonfigurationJson LadeKonfiguration()
    {
        string jsonString = KonfigurationJsonExport.Rezeption;
        var config = JsonSerializer.Deserialize<RezeptionKonfigurationJson>(jsonString);
        return config ?? throw new InvalidOperationException("Rezeption-Konfiguration konnte nicht geladen werden.");
    }
}

internal sealed class RezeptionKonfigurationJson
{
    public int Anzahl { get; set; }
}
