using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AirlineTicketOffice.Core.Extensions;
using AirlineTicketOffice.Wpf.Services;
using AirlineTicketOffice.Wpf.ViewModels;
using AirlineTicketOffice.Wpf.Views;

namespace AirlineTicketOffice.Wpf;

/// <summary>
/// Главный класс приложения WPF. Отвечает за инициализацию DI-контейнера,
/// миграции БД и запуск стартового окна.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Глобальный провайдер сервисов внедрения зависимостей (DI).
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // 1. Регистрация ядра Core (M1–M15) и базы данных SQLite
        services.AddAirlineCore("Data Source=airline.db");

        // 2. Регистрация сервиса навигации
        services.AddSingleton<INavigationService, NavigationService>();

        // 3. Регистрация моделей представления (ViewModels)
        services.AddTransient<RoleSelectionViewModel>();
        services.AddTransient<MainViewModel>();

        // 4. Регистрация окон и представлений (Views)
        services.AddTransient<RoleSelectionWindow>();
        services.AddTransient<MainWindow>();

        // Формирование провайдера сервисов
        Services = services.BuildServiceProvider();

        // Гарантированная инициализация структуры базы данных и сидирование справочников
        Services.InitializeAirlineDatabase();

        // Отображение стартового окна выбора роли W-01
        var navService = Services.GetRequiredService<INavigationService>();
        navService.OpenRoleSelectionWindow();
    }
}
