// MystTiq v1.0.8.5: file reviewed for this release (2026-10-12).
using System.Windows.Input;
using Avalonia.Threading;

namespace MystTiq.Desktop.ViewModels;

// v1.0.8.0 (roadmap C-0): the commands every ViewModel uses, moved out of MainWindowViewModel.cs unchanged.
// v0.8.23.0: a command the signed-in role may not use cannot run (see MainWindowViewModel.CommandRoles.cs).
internal interface IRoleGatedCommand
{
    Func<bool>? RoleGate { get; set; }
    // v0.9.4.0: the role the command needs and the signed-in role, so a control it is disabled on can say why (RoleHint).
    string? RequiredRole { get; set; }
    Func<string?>? SignedInRole { get; set; }
    void RaiseCanExecuteChanged();
}

internal sealed class RelayCommand : ICommand, IRoleGatedCommand
{
    public Func<bool>? RoleGate { get; set; }
    public string? RequiredRole { get; set; }
    public Func<string?>? SignedInRole { get; set; }
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => (RoleGate?.Invoke() ?? true) && (_canExecute?.Invoke() ?? true);
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class RelayCommand<T> : ICommand, IRoleGatedCommand
{
    public Func<bool>? RoleGate { get; set; }
    public string? RequiredRole { get; set; }
    public Func<string?>? SignedInRole { get; set; }
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => (RoleGate?.Invoke() ?? true) && (_canExecute?.Invoke(parameter is T typed ? typed : default) ?? true);

    public void Execute(object? parameter)
    {
        if (parameter is T typed)
            _execute(typed);
        else
            _execute(default);
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class AsyncCommand : ICommand, IRoleGatedCommand
{
    public Func<bool>? RoleGate { get; set; }
    public string? RequiredRole { get; set; }
    public Func<string?>? SignedInRole { get; set; }
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_running && (RoleGate?.Invoke() ?? true) && (_canExecute?.Invoke() ?? true);
    public event EventHandler? CanExecuteChanged;

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _running = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
