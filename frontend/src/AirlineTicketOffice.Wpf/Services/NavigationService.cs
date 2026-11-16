using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AirlineTicketOffice.Core.Contracts.Documents;
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

    /// <inheritdoc />
    public void OpenDocumentPreview(PrintDocumentDto document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var previewVm = _serviceProvider.GetRequiredService<DocumentPreviewViewModel>();
        previewVm.Initialize(document);

        var previewWindow = _serviceProvider.GetRequiredService<DocumentPreviewWindow>();
        previewWindow.DataContext = previewVm;
        previewVm.CloseRequested += () => previewWindow.Close();

        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            previewWindow.Owner = Application.Current.MainWindow;
        }

        previewWindow.ShowDialog();
    }

    /// <inheritdoc />
    public void OpenRefundDialog(int ticketId)
    {
        var refundVm = _serviceProvider.GetRequiredService<RefundTicketViewModel>();
        refundVm.Initialize(ticketId);

        var refundWindow = _serviceProvider.GetRequiredService<RefundTicketWindow>();
        refundWindow.DataContext = refundVm;
        refundVm.CloseRequested += () => refundWindow.Close();

        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            refundWindow.Owner = Application.Current.MainWindow;
        }

        refundWindow.ShowDialog();
    }
}
