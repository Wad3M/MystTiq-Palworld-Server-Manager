// MystTiq v1.1.2.0: file reviewed for this release (2026-10-19).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

public interface IConnectionProfileStore
{
    IReadOnlyList<ConnectionProfile> Load();
    void Save(IEnumerable<ConnectionProfile> profiles);
    string StoragePath { get; }
}
