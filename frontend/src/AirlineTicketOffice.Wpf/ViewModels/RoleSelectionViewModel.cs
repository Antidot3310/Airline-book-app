using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// ViewModel окна выбора роли (W-01).
/// Открывает сеанс выбранной роли через <see cref="ISessionService"/> и переходит к главному окну.
/// </summary>
public partial class RoleSelectionViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="RoleSelectionViewModel"/>.
    /// </summary>
    /// <param name="sessionService">Сервис управления сеансами и ролями.</param>
    /// <param name="navigationService">Сервис навигации между окнами.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если обязательная зависимость равна null.</exception>
    public RoleSelectionViewModel(ISessionService sessionService, INavigationService navigationService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    /// <summary>
    /// Открывает сеанс роли «Кассир» и запускает главное окно приложения.
    /// </summary>
    [RelayCommand]
    private void LoginAsCashier()
    {
        OpenRoleSession(UserRole.Cashier);
    }

    /// <summary>
    /// Открывает сеанс роли «Администратор» и запускает главное окно приложения.
    /// </summary>
    [RelayCommand]
    private void LoginAsAdministrator()
    {
        OpenRoleSession(UserRole.Administrator);
    }

    private void OpenRoleSession(UserRole role)
    {
        try
        {
            ErrorMessage = string.Empty;
            _sessionService.OpenSession(role);
            _navigationService.NavigateToMainShell();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Не удалось открыть сеанс: {exception.Message}";
        }
    }
}
