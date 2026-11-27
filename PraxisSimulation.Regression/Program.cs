using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using simSharpSimulation;

internal static class Program
{
    public static void Main()
    {
        var alteKultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            KonfigurationsRegression.Pruefe();
            PruefeLaufgrenzen();
            PruefeSzenario("Standard",
                "ABDAD7C7D437705E6094FDEE983D37B709C8E1A819CD4B633DF732D8A5892A38", 42, 1);
            PruefeSzenario("Personalengpass",
                "D2E1F437C08D05F19727D9CA3DB8E068529154242E53F2B45572595D8B5077E7", 17, 3, (1, 1, 1), (3, 3));
            PruefeSzenario("Raumengpass",
                "DFCB164C1425B6AB7EF4099D2AC255A9AFCC52E35703B11651C15C96FFD3C71F", 17, 3, (4, 3, 2), (1, 1));
            PruefeSzenario("Mehr Kapazitaet",
                "C4E4899F99060410CEA9697A12BCE6AF1611D3ADEC09AFCE9542F8FEABF6E465", 17, 3, (4, 3, 2), (3, 3));
        }
        finally
        {
            KonfigurationJsonExport.LadeAlle();
            CultureInfo.CurrentCulture = alteKultur;
        }
    }

    private static void PruefeLaufgrenzen()
    {
        var daten = new SimulationsDaten();
        var prozess = new PatientenProzess(42, daten);
        prozess.FuehreAus(0);
        if (daten.TraceData.Count != 0)
            throw new InvalidOperationException("Ein Lauf mit null Tagen muss leer bleiben.");

        try
        {
            prozess.FuehreAus(-1);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "arbeitstage")
        {
            Console.WriteLine("Laufgrenzen bestanden.");
            return;
        }

        throw new InvalidOperationException("Negative Arbeitstage wurden akzeptiert.");
    }

    private static void PruefeSzenario(string name, string referenzHash, int seed, int tage,
        (int Aerzte, int Schwestern, int Rezeption)? personal = null,
        (int Arzt, int Schwester)? raeume = null)
    {
        KonfigurationJsonExport.LadeAlle();
        var fixkosten = KonfigurationJsonExport.Finanzen.Fixkosten;
        if (personal is { } p)
        {
            ArztKonfiguration.ANZAHL_AERZTE = p.Aerzte;
            SchwesterKonfiguration.ANZAHL_SCHWESTERN = p.Schwestern;
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = p.Rezeption;
        }
        if (raeume is { } r)
        {
            fixkosten.AnzahlBehandlungsraeumeArzt = r.Arzt;
            fixkosten.AnzahlBehandlungsraeumeSchwester = r.Schwester;
        }

        var daten = new SimulationsDaten();
        var events = new List<SimulationLiveEvent>();
        daten.LiveEventOccurred += events.Add;
        new PatientenProzess(seed, daten).FuehreAus(tage);
        var temp = Path.GetTempFileName();
        try
        {
            daten.SchreibePrognoseDatenJson(temp);
            using var prognose = JsonDocument.Parse(File.ReadAllText(temp));
            var vergleich = new
            {
                Events = events,
                Statistik = daten,
                HitMiss = daten.HitMissProTag,
                Prognose = prognose.RootElement,
                Finanzen = daten.HitMissProTag.Select(tag =>
                    FinanzRechner.BerechneTagesergebnis(ArztKonfiguration.ANZAHL_AERZTE, tag.Hit))
            };
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(vergleich)));
            if (hash != referenzHash)
                throw new InvalidOperationException($"{name}: Erwartet {referenzHash}, erhalten {hash}.");
            Console.WriteLine($"{name} (Seed {seed}, {tage} Tage): {events.Count} Events, {daten.AnzahlBehandeltHit} Hit, {daten.AnzahlAbgebrochenMiss} Miss, {hash}");
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
