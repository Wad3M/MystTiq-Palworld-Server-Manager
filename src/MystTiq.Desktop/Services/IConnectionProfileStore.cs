// MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

public interface IConnectionProfileStore
{
    IReadOnlyList<ConnectionProfile> Load();
    void Save(IEnumerable<ConnectionProfile> profiles);
    string StoragePath { get; }
}
