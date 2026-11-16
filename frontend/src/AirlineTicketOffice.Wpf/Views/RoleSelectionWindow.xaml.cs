using System.Windows;

namespace AirlineTicketOffice.Wpf.Views;

/// <summary>
/// Окно выбора роли (W-01). Вся логика и привязки находятся в <see cref="AirlineTicketOffice.Wpf.ViewModels.RoleSelectionViewModel"/>.
/// </summary>
public partial class RoleSelectionWindow : Window
{
    public RoleSelectionWindow()
    {
        InitializeComponent();
    }
}
