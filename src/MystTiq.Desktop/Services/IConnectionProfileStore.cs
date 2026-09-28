// MystTiq v0.8.26.0: file reviewed for this release (2026-09-27).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

public interface IConnectionProfileStore
{
    IReadOnlyList<ConnectionProfile> Load();
    void Save(IEnumerable<ConnectionProfile> profiles);
    string StoragePath { get; }
}
