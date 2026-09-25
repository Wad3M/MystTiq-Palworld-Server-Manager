namespace MystTiq.Desktop.Services;

// v0.7.96.0: the one-line "what is on the map" summary under the World Map. Pure, so the logic
// harness can compile this file directly. Player dots come from the running server's REST API and
// bases come from the world save, so an empty player list still says whether bases are drawn.
// v0.7.100.0: offline players are drawn at their last known position from the save, and say so.
public static class MapContentsText
{
    public static string Describe(int players, int located, int bases, int offline = 0)
    {
        var offlineText = offline > 0 ? $" {offline} offline player(s) shown at their last known position from the save." : string.Empty;
        var baseText = bases > 0 ? $" {bases} base(s) shown from the world save." : string.Empty;
        if (players == 0)
            return "No players are online, or the server is stopped, so there are no live player dots." + offlineText + baseText;
        if (located == 0)
            return $"{players} player(s) online, but the server reported no position for them yet." + offlineText + baseText;
        var missing = players - located;
        var head = $"{located} player(s) online on the map";
        if (missing > 0) head += $", {missing} without a reported position";
        return head + "." + offlineText + baseText;
    }
}
