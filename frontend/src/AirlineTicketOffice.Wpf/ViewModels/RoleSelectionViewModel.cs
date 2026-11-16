using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления стартового окна выбора роли W-01.
/// </summary>
public partial class RoleSelectionViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoleSelectionViewModel"/>.
    /// </summary>
    /// <param name="sessionService">Сервис управления пользовательскими сессиями.</param>
    /// <param name="navigationService">Сервис навигации между окнами.</param>
    public RoleSelectionViewModel(
        ISessionService sessionService,
        INavigationService navigationService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    /// <summary>
    /// Выбирает роль пользователя, открывает сеанс и переходит в главное окно W-02.
    /// </summary>
    /// <param name="parameter">Роль пользователя (UserRole или строковое имя роли).</param>
    [RelayCommand]
    public void SelectRole(object? parameter)
    {
        UserRole role = parameter switch
        {
            UserRole r => r,
            string s when Enum.TryParse<UserRole>(s, true, out var parsedRole) => parsedRole,
            _ => UserRole.Passenger
        };

        _sessionService.OpenSession(role);
        _navigationService.OpenMainWindow();
    }

    /// <summary>
    /// Команда быстрого входа под ролью Пассажира.
    /// </summary>
    [RelayCommand]
    public void SelectPassenger()
    {
        SelectRole(UserRole.Passenger);
    }

    /// <summary>
    /// Команда быстрого входа под ролью Кассира.
    /// </summary>
    [RelayCommand]
    public void SelectCashier()
    {
        SelectRole(UserRole.Cashier);
    }

    /// <summary>
    /// Команда быстрого входа под ролью Администратора.
    /// </summary>
    [RelayCommand]
    public void SelectAdministrator()
    {
        SelectRole(UserRole.Administrator);
    }

    /// <summary>
    /// Команда открытия служебного окна эмулятора (W-16).
    /// </summary>
    [RelayCommand]
    public void OpenEmulator()
    {
        _navigationService.OpenEmulatorWindow();
    }
}
