// MystTiq v1.1.4.0: file reviewed for this release (2026-10-20).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.7.2 (roadmap Q-7): the connection line says what is actually wrong, with a next step.
public sealed partial class MainWindowViewModel
{
    private string _connectionCauseText = string.Empty;
    private bool _wasConnected;
    private int _connectionFailures;

    public string ConnectionCauseText { get => _connectionCauseText; private set { if (SetField(ref _connectionCauseText, value)) RaisePropertyChanged(nameof(HasConnectionCause)); } }
    public bool HasConnectionCause => ConnectionCauseText.Length > 0;

    private void NoteConnected(ServerStatusDto status, PlayersSnapshotDto players)
    {
        _wasConnected = true;
        _connectionFailures = 0;
        ConnectionCauseText = ConnectionCause.ForConnected(status.Ready, players.Available, players.Detail);
    }

    private void NoteConnectionFailure(Exception error, Uri? address)
    {
        _connectionFailures++;
        var (state, cause) = ConnectionCause.ForFailure(error, _wasConnected, _connectionFailures, address);
        ConnectionState = state;
        ConnectionCauseText = cause;
    }
}
