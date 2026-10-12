using System;
using System.IO;

namespace simSharpSimulation;

internal static class ProjektPfade
{
    internal static string ErmittleProjektOrdner()
    {
        return FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj")
            ?? FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj")
            ?? Directory.GetCurrentDirectory();
    }

    internal static string? FindeOrdnerMitDatei(string startPfad, string dateiname)
    {
        DirectoryInfo? ordner = new(startPfad);
        while (ordner != null)
        {
            if (File.Exists(Path.Combine(ordner.FullName, dateiname)))
                return ordner.FullName;

            ordner = ordner.Parent;
        }

        return null;
    }
}
