using System;
using System.IO;
using ComingUpNextTray.Services;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: CalendarInspector <path-to-ics>");
            return 2;
        }

        string path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        string ics = File.ReadAllText(path);
    var diag = ComingUpNextTray.Services.CalendarDiagnostics.Inspect(ics, DateTime.Now);

        Console.WriteLine($"Raw VEVENTs: {diag.RawEvents.Count}");
        for (int i = 0; i < diag.RawEvents.Count; i++)
        {
            Console.WriteLine($"--- VEVENT #{i + 1} ---");
            Console.WriteLine(diag.RawEvents[i]);
            Console.WriteLine();
        }

        Console.WriteLine($"Parsed entries: {diag.Entries.Count}");
        foreach (string e in diag.Entries)
        {
            Console.WriteLine(e);
        }

        Console.WriteLine("Expansion log:");
        foreach (var l in diag.ExpansionLog)
        {
            Console.WriteLine(l);
        }

        return 0;
    }
}
