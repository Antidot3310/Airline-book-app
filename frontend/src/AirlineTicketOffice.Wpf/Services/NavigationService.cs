using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AirlineTicketOffice.Wpf.ViewModels;
using AirlineTicketOffice.Wpf.Views;

namespace AirlineTicketOffice.Wpf.Services;

/// <summary>
/// Реализация сервиса навигации между окнами и представлениями WPF-приложения.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public void OpenMainWindow()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();

        // Закрываем окно выбора роли
        foreach (Window window in Application.Current.Windows)
        {
            if (window is RoleSelectionWindow && window != mainWindow)
            {
                window.Close();
                break;
            }
        }
    }

    /// <inheritdoc />
    public void OpenRoleSelectionWindow()
    {
        var roleWindow = _serviceProvider.GetRequiredService<RoleSelectionWindow>();
        roleWindow.DataContext = _serviceProvider.GetRequiredService<RoleSelectionViewModel>();
        roleWindow.Show();

        // Закрываем главное окно
        foreach (Window window in Application.Current.Windows)
        {
            if (window is MainWindow && window != roleWindow)
            {
                window.Close();
                break;
            }
        }
    }

    /// <inheritdoc />
    public void OpenEmulatorWindow()
    {
        MessageBox.Show(
            "Служебный пульт управления эмулятором (W-16) будет подключен в Инкременте 40.\n" +
            "В нём будут доступны: сдвиг виртуального времени, выбор исходов оплаты и ручной резолв таймаутов.",
            "Эмулятор W-16",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
