using System.Windows;
using AirlineTicketOffice.Wpf.ViewModels;
using AirlineTicketOffice.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Wpf.Services;

/// <summary>
/// Реализация сервиса навигации: создаёт окна через DI, назначает ViewModel и закрывает предыдущее окно.
/// Единая область видимости DI сохраняет активный сеанс пользователя между окнами W-01 и W-02.
/// </summary>
public sealed class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;

    private IServiceScope? _applicationScope;
    private Window? _currentWindow;
    private bool _disposed;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="NavigationService"/>.
    /// </summary>
    /// <param name="scopeFactory">Фабрика областей видимости DI.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="scopeFactory"/> равен null.</exception>
    public NavigationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    private IServiceProvider Services => (_applicationScope ??= _scopeFactory.CreateScope()).ServiceProvider;

    /// <inheritdoc />
    public void NavigateToRoleSelection()
    {
        ShowWindow<RoleSelectionWindow, RoleSelectionViewModel>();
    }

    /// <inheritdoc />
    public void NavigateToMainShell()
    {
        ShowWindow<MainShellWindow, MainShellViewModel>();
    }

    private void ShowWindow<TWindow, TViewModel>()
        where TWindow : Window
        where TViewModel : class
    {
        Application application = Application.Current
            ?? throw new InvalidOperationException("WPF-приложение не инициализировано.");

        TWindow window = Services.GetRequiredService<TWindow>();
        window.DataContext = Services.GetRequiredService<TViewModel>();

        Window? previousWindow = _currentWindow;
        _currentWindow = window;

        application.MainWindow = window;
        window.Show();

        if (previousWindow is not null && !ReferenceEquals(previousWindow, window))
        {
            if (previousWindow.DataContext is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }

            previousWindow.Close();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _applicationScope?.Dispose();
        _applicationScope = null;
        _disposed = true;
    }
}
