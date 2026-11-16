using System.Windows;

namespace AirlineTicketOffice.Wpf.Views;

/// <summary>
/// Главное окно приложения (W-02). Вся логика и привязки находятся в <see cref="AirlineTicketOffice.Wpf.ViewModels.MainShellViewModel"/>.
/// </summary>
public partial class MainShellWindow : Window
{
    public MainShellWindow()
    {
        InitializeComponent();
    }
}
