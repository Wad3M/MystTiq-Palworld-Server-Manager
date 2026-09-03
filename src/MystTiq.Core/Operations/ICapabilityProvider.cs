namespace MystTiq.Core.Operations;

// Minimal provider/capability contract. Existing platform-abstraction
// services (e.g. IServerDistributionPlatformService's Windows/Linux
// implementations) can implement this alongside their existing interface to
// advertise themselves as a discoverable capability, without forcing every
// current platform service to be rewritten around a heavier provider
// framework before that framework's own milestone (v0.6.2.0) arrives.
public interface ICapabilityProvider
{
    string ProviderId { get; }
    bool IsAvailable { get; }
}
