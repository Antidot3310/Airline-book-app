using System.IO;
using System.Windows;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Extensions;
using AirlineTicketOffice.Wpf.Services;
using AirlineTicketOffice.Wpf.ViewModels;
using AirlineTicketOffice.Wpf.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Wpf;

/// <summary>
/// Точка входа WPF-приложения: настройка DI-контейнера, инициализация базы данных и запуск окна W-01.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ServiceCollection services = new();
            string databasePath = Path.Combine(AppContext.BaseDirectory, "airline.db");

            services.AddAirlineCore($"Data Source={databasePath}");
            services.AddSingleton<INavigationService, NavigationService>();

            services.AddTransient<RoleSelectionViewModel>();
            services.AddTransient<MainShellViewModel>();
            services.AddTransient<RoleSelectionWindow>();
            services.AddTransient<MainShellWindow>();

            _serviceProvider = services.BuildServiceProvider();

            using (IServiceScope scope = _serviceProvider.CreateScope())
            {
                AirlineDbContext dbContext = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
                dbContext.Database.EnsureCreated();
                DbInitializer.Initialize(dbContext);
            }

            INavigationService navigationService = _serviceProvider.GetRequiredService<INavigationService>();
            navigationService.NavigateToRoleSelection();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Не удалось запустить приложение: {exception.Message}",
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
