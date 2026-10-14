// MystTiq v1.1.1.0: file reviewed for this release (2026-10-14).
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.9 (roadmap C-9): the Security page -- API principals (tokens), named user accounts and the browser view's
/// address -- moved out of MainWindowViewModel unchanged. The window shows it as vm.Security; signing in, who is signed in
/// and the role gates stay with the window (every page and the Ribbon follow them).
/// </summary>
public sealed class SecurityViewModel : PageViewModel
{
    private Action<MystTiqPrincipalDto?> ApplyCurrentPrincipal { get; }
    private Func<bool> CanManagePrincipals { get; }

    public SecurityViewModel(SessionContext session, Action<MystTiqPrincipalDto?> applyCurrentPrincipal, Func<bool> canManagePrincipals) : base(session, NavigationPage.Security)
    {
        ApplyCurrentPrincipal = applyCurrentPrincipal;
        CanManagePrincipals = canManagePrincipals;
        RefreshSecurityCommand = new AsyncCommand(RefreshSecurityAsync, () => !Session.IsBusy);
        CreatePrincipalCommand = new AsyncCommand(CreatePrincipalAsync, () => !Session.IsBusy && !string.IsNullOrWhiteSpace(NewPrincipalName));
        RevokePrincipalCommand = new AsyncCommand(RevokePrincipalAsync, () => !Session.IsBusy && SelectedPrincipal is not null);
        CreateUserCommand = new AsyncCommand(CreateUserAsync, () => !Session.IsBusy);
        ResetUserPasswordCommand = new AsyncCommand(ResetUserPasswordAsync, () => !Session.IsBusy);
        ToggleUserEnabledCommand = new AsyncCommand(ToggleUserEnabledAsync, () => !Session.IsBusy);
        DeleteUserCommand = new AsyncCommand(DeleteUserAsync, () => !Session.IsBusy);
    }

    private string _securityState = "Open Security to load API principals.";

    private MystTiqPrincipalDto? _selectedPrincipal;

    private string _newPrincipalName = string.Empty;

    private string _newPrincipalRole = "Operator";

    private string? _lastCreatedPrincipalToken;

    public ObservableCollection<MystTiqPrincipalDto> Principals { get; } = [];

    public string SecurityState { get => _securityState; private set => SetField(ref _securityState, value); }

    public MystTiqPrincipalDto? SelectedPrincipal { get => _selectedPrincipal; set => SetField(ref _selectedPrincipal, value); }

    public string NewPrincipalName { get => _newPrincipalName; set { if (SetField(ref _newPrincipalName, value ?? string.Empty)) (CreatePrincipalCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string NewPrincipalRole { get => _newPrincipalRole; set => SetField(ref _newPrincipalRole, value ?? "Operator"); }

    public string? LastCreatedPrincipalToken { get => _lastCreatedPrincipalToken; private set { if (SetField(ref _lastCreatedPrincipalToken, value)) RaisePropertyChanged(nameof(HasLastCreatedPrincipalToken)); } }

    public bool HasLastCreatedPrincipalToken => !string.IsNullOrEmpty(LastCreatedPrincipalToken);

    public IReadOnlyList<string> MystTiqRoles { get; } = ["Viewer", "Operator", "Admin", "Owner"];

    public ICommand RefreshSecurityCommand { get; }

    public ICommand CreatePrincipalCommand { get; }

    public ICommand RevokePrincipalCommand { get; }

    internal async Task RefreshSecurityAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var who = await Session.Api.WhoAmIAsync(Session.SelectedProfile, Session.BearerToken);
            ApplyCurrentPrincipal(who);
            await RefreshUserAccountsAsync();

            var principals = await Session.Api.GetPrincipalsAsync(Session.SelectedProfile, Session.BearerToken);
            var selectedId = SelectedPrincipal?.Id;
            Principals.Clear(); foreach (var principal in principals) Principals.Add(principal);
            SelectedPrincipal = Principals.FirstOrDefault(x => x.Id == selectedId);
            SecurityState = $"Signed in as {who.Name} ({who.Role}). {Principals.Count} principal(s) issued.";
        }
        catch (Exception ex) { SecurityState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task CreatePrincipalAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.CreatePrincipalAsync(Session.SelectedProfile, new HeadlessCreatePrincipalRequestDto(NewPrincipalName, NewPrincipalRole, null), Session.BearerToken);
            Principals.Add(result.Principal);
            LastCreatedPrincipalToken = result.PlaintextToken;
            NewPrincipalName = string.Empty;
            SecurityState = $"Created principal '{result.Principal.Name}'. Copy the token now — it will not be shown again.";
        }
        catch (Exception ex) { SecurityState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RevokePrincipalAsync()
    {
        if (Session.SelectedProfile is null || SelectedPrincipal is not { } principal) return;
        Session.IsBusy = true;
        try
        {
            await Session.Api.RevokePrincipalAsync(Session.SelectedProfile, principal.Id, Session.BearerToken);
            Principals.Remove(principal);
            SelectedPrincipal = null;
            SecurityState = $"Revoked principal '{principal.Name}'.";
        }
        catch (Exception ex) { SecurityState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private string _newUserUsername = string.Empty;

    private string _newUserDisplayName = string.Empty;

    private string _newUserPassword = string.Empty;

    private string _newUserRole = "Operator";

    private string _newUserScope = string.Empty;

    private string _userResetPassword = string.Empty;

    private string _userAccountsState = string.Empty;

    private UserAccountDto? _selectedUserAccount;

    public string NewUserUsername { get => _newUserUsername; set => SetField(ref _newUserUsername, value ?? string.Empty); }

    public string NewUserDisplayName { get => _newUserDisplayName; set => SetField(ref _newUserDisplayName, value ?? string.Empty); }

    public string NewUserPassword { get => _newUserPassword; set => SetField(ref _newUserPassword, value ?? string.Empty); }

    public string NewUserRole { get => _newUserRole; set => SetField(ref _newUserRole, value ?? "Operator"); }

    public string NewUserScope { get => _newUserScope; set => SetField(ref _newUserScope, value ?? string.Empty); }

    public string UserResetPassword { get => _userResetPassword; set => SetField(ref _userResetPassword, value ?? string.Empty); }

    public string UserAccountsState { get => _userAccountsState; set => SetField(ref _userAccountsState, value ?? string.Empty); }

    public UserAccountDto? SelectedUserAccount { get => _selectedUserAccount; set => SetField(ref _selectedUserAccount, value); }

    public ObservableCollection<UserAccountDto> UserAccounts { get; } = [];

    public ICommand CreateUserCommand { get; private set; } = null!;

    public ICommand ResetUserPasswordCommand { get; private set; } = null!;

    public ICommand ToggleUserEnabledCommand { get; private set; } = null!;

    public ICommand DeleteUserCommand { get; private set; } = null!;

    private async Task RefreshUserAccountsAsync()
    {
        if (Session.SelectedProfile is null || !CanManagePrincipals()) { UserAccounts.Clear(); return; }
        var selectedId = SelectedUserAccount?.Id;
        var users = await Session.Api.GetUsersAsync(Session.SelectedProfile, Session.BearerToken);
        UserAccounts.Clear();
        foreach (var user in users) UserAccounts.Add(user);
        SelectedUserAccount = UserAccounts.FirstOrDefault(u => u.Id == selectedId);
        UserAccountsState = $"{UserAccounts.Count} user account(s).";
    }

    private async Task RunUserAccountActionAsync(Func<Task<UserAccountResultDto>> action)
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await action();
            UserAccountsState = result.Message;
            if (result.Success) await RefreshUserAccountsAsync();
            if (result.Success) UserAccountsState = result.Message;
        }
        catch (Exception ex) { UserAccountsState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private Task CreateUserAsync() => RunUserAccountActionAsync(async () =>
    {
        var result = await Session.Api.CreateUserAsync(Session.SelectedProfile!, NewUserUsername.Trim(), NewUserDisplayName.Trim(), NewUserRole, NewUserPassword,
            string.IsNullOrWhiteSpace(NewUserScope) ? null : NewUserScope.Trim(), Session.BearerToken);
        if (result.Success) { NewUserUsername = string.Empty; NewUserDisplayName = string.Empty; NewUserPassword = string.Empty; }
        return result;
    });

    private Task ResetUserPasswordAsync()
    {
        if (SelectedUserAccount is not { } user) { UserAccountsState = "Select an account first."; return Task.CompletedTask; }
        return RunUserAccountActionAsync(async () =>
        {
            var result = await Session.Api.SetUserPasswordAsync(Session.SelectedProfile!, user.Id, UserResetPassword, Session.BearerToken);
            if (result.Success) UserResetPassword = string.Empty;
            return result;
        });
    }

    private Task ToggleUserEnabledAsync()
    {
        if (SelectedUserAccount is not { } user) { UserAccountsState = "Select an account first."; return Task.CompletedTask; }
        return RunUserAccountActionAsync(() => Session.Api.UpdateUserAsync(Session.SelectedProfile!, user.Id, user.DisplayName, user.Role, user.ScopedServerProfileId, !user.Enabled, Session.BearerToken));
    }

    private Task DeleteUserAsync()
    {
        if (SelectedUserAccount is not { } user) { UserAccountsState = "Select an account first."; return Task.CompletedTask; }
        return RunUserAccountActionAsync(() => Session.Api.DeleteUserAsync(Session.SelectedProfile!, user.Id, Session.BearerToken));
    }

    // v1.0.3.0 (roadmap W-1): the read-only browser view lives at the same address as the API, under /web.
    public string BrowserViewUrlVerbatim => Session.SelectedProfile is { } profile ? new Uri(profile.BaseAddress, "/web").ToString() : "—";
}
