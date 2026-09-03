using System.Text.Json.Serialization;

namespace MystTiq.Core.Operations;

// Canonical server/profile identity seam for the eventual multi-server fleet
// (roadmap v0.6.8.0). MystTiq is single-server today, so every caller uses
// Default -- but every operation record, lock, and future API already carries
// a real ServerProfileId instead of relying on process name or an implicit
// "the one server", so v0.6.8.0 does not require another rewrite of this
// plumbing (programming rule: "Multi-server identity never relies only on
// process name").
[JsonConverter(typeof(ServerProfileIdJsonConverter))]
public readonly record struct ServerProfileId(string Value)
{
    public static readonly ServerProfileId Default = new("default");
    public override string ToString() => Value;
}
