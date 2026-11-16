using System.Windows;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Models;
using AirlineTicketOffice.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// ViewModel главного окна приложения (W-02).
/// Формирует динамическое меню по текущей роли и синхронизирует строку состояния
/// с виртуальным временем через событие <see cref="IVirtualClockService.TimeChanged"/>.
/// </summary>
public partial class MainShellViewModel : ObservableObject, IDisposable
{
    private const string VirtualTimeFormat = "dd.MM.yyyy HH:mm";

    private readonly ISessionService _sessionService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;

    private bool _disposed;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _roleDisplay = string.Empty;

    [ObservableProperty]
    private string _virtualTimeDisplay = string.Empty;

    [ObservableProperty]
    private NavigationMenuItem? _selectedMenuItem;

    /// <summary>
    /// Коллекция разделов меню, доступных текущей роли.
    /// </summary>
    public IReadOnlyList<NavigationMenuItem> MenuItems { get; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="MainShellViewModel"/>.
    /// </summary>
    /// <param name="sessionService">Сервис управления сеансами и ролями.</param>
    /// <param name="clockService">Сервис виртуального времени системы.</param>
    /// <param name="navigationService">Сервис навигации между окнами.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если обязательная зависимость равна null.</exception>
    public MainShellViewModel(
        ISessionService sessionService,
        IVirtualClockService clockService,
        INavigationService navigationService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        UserRole currentRole = _sessionService.CurrentRole;

        RoleDisplay = GetRoleDisplay(currentRole);
        WindowTitle = $"Авиакасса — {RoleDisplay}";
        MenuItems = BuildMenu(currentRole);
        SelectedMenuItem = MenuItems.Count > 0 ? MenuItems[0] : null;
        VirtualTimeDisplay = FormatVirtualTime(_clockService.CurrentVirtualTime);

        _clockService.TimeChanged += OnTimeChanged;
    }

    /// <summary>
    /// Закрывает текущий сеанс и возвращает пользователя к окну выбора роли (W-01).
    /// </summary>
    [RelayCommand]
    private void ChangeRole()
    {
        try
        {
            _sessionService.CloseSession();
        }
        catch (Exception)
        {
            // Ошибка журналирования закрытия сеанса не должна блокировать смену роли.
        }

        _navigationService.NavigateToRoleSelection();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _clockService.TimeChanged -= OnTimeChanged;
        _disposed = true;
    }

    private void OnTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        Application? application = Application.Current;

        if (application is null)
        {
            return;
        }

        application.Dispatcher.Invoke(() => VirtualTimeDisplay = FormatVirtualTime(e.NewVirtualTime));
    }

    private static string FormatVirtualTime(DateTime value)
    {
        return value.ToString(VirtualTimeFormat);
    }

    private static string GetRoleDisplay(UserRole role)
    {
        return role switch
        {
            UserRole.Passenger => "Пассажир",
            UserRole.Cashier => "Кассир",
            UserRole.Administrator => "Администратор",
            _ => "Система"
        };
    }

    private static IReadOnlyList<NavigationMenuItem> BuildMenu(UserRole role)
    {
        return role switch
        {
            UserRole.Cashier =>
            [
                new("Поиск рейсов", "Подбор рейсов по маршруту, дате и количеству пассажиров."),
                new("Оформление заказа", "Бронирование мест, ввод данных пассажиров и оплата."),
                new("Возврат билета", "Расчёт суммы возврата и формирование чека."),
                new("Клиенты", "Карточки клиентов и история оформленных заказов.")
            ],

            UserRole.Administrator =>
            [
                new("Справочники", "Аэропорты, типы воздушных судов и парк бортов."),
                new("Рейсы", "Создание и отмена рейсов, назначение воздушных судов."),
                new("Тарифы", "Базовые цены направлений и коэффициенты расчёта."),
                new("Отчёты", "Загрузка рейсов, выручка и статистика возвратов."),
                new("Журнал аудита", "Просмотр журнала системных операций и действий ролей.")
            ],

            UserRole.Passenger =>
            [
                new("Поиск рейсов", "Подбор рейсов по маршруту, дате и количеству пассажиров."),
                new("Мои билеты", "Предстоящие, прошедшие и возвращённые билеты текущего сеанса.")
            ],

            _ =>
            [
                new("Главное", "Рабочая область приложения.")
            ]
        };
    }
}
