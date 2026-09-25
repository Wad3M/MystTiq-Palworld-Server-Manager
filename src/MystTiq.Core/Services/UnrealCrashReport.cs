using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MystTiq.Core.Services;

// v0.8.9.0: Unreal Engine writes a crash report folder for every crash (Pal\Saved\Crashes\UECC-<platform>-<id>_<n>\) with
// a CrashContext.runtime-xml naming the error, the crash type and the engine version. It is the most exact crash
// evidence a Palworld server leaves -- the log may be cut off mid-line, the report is not -- but the Crash Analyzer only
// ever read the logs. This parses one report into a single evidence line the signature catalog classifies like any log
// line, stamped with the time the report was written. Pure, so the logic harness covers it.
public sealed record UnrealCrashReport(string Folder, DateTimeOffset WrittenAt, string ErrorMessage, string CrashType, string EngineVersion);

public static class UnrealCrashReportParser
{
    public const string ContextFileName = "CrashContext.runtime-xml";

    // Null when the document is not a crash context or carries no error message.
    public static UnrealCrashReport? Parse(string folder, DateTimeOffset writtenAt, string xml)
    {
        XDocument document;
        try { document = XDocument.Parse(xml, LoadOptions.None); }
        catch (XmlException) { return null; }

        var properties = document.Root?.Element("RuntimeProperties");
        if (properties is null) return null;
        var error = Flatten(properties.Element("ErrorMessage")?.Value);
        if (error.Length == 0) return null;
        return new UnrealCrashReport(folder, writtenAt, error,
            Flatten(properties.Element("CrashType")?.Value), Flatten(properties.Element("EngineVersion")?.Value));
    }

    // "[2026.09.16-10.04.54] Unreal crash report UECC-...: <error> (Assert, engine 5.1.1-0+++UE5+Release-5.1)", in the
    // server's local time like the game's own log stamps, so CrashSignatureCatalog.TryParseTimestamp reads it back.
    public static string ToEvidenceLine(UnrealCrashReport report)
    {
        var details = string.Join(", ", new[] { report.CrashType, report.EngineVersion.Length > 0 ? "engine " + report.EngineVersion : string.Empty }
            .Where(s => s.Length > 0));
        return string.Create(CultureInfo.InvariantCulture,
            $"[{report.WrittenAt.ToLocalTime():yyyy.MM.dd-HH.mm.ss}] Unreal crash report {report.Folder}: {report.ErrorMessage}{(details.Length > 0 ? $" ({details})" : string.Empty)}");
    }

    private static string Flatten(string? value)
    {
        var text = string.Join(' ', (value ?? string.Empty).Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0));
        return text.Length > 400 ? text[..400] : text;
    }
}
