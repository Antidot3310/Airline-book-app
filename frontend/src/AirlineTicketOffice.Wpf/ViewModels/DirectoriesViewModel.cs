using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Справочники авиакомпании» W-11.
/// Обеспечивает ведение справочников аэропортов, флота воздушных судов и типов ВС
/// с контролем ссылочной целостности и запретов на деактивацию.
/// </summary>
public partial class DirectoriesViewModel : ObservableObject
{
    private readonly IDirectoryService _directoryService;

    [ObservableProperty]
    private int _selectedTab;

    public bool IsTabAirports => SelectedTab == 0;
    public bool IsTabAircrafts => SelectedTab == 1;
    public bool IsTabAircraftTypes => SelectedTab == 2;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTabAirports));
        OnPropertyChanged(nameof(IsTabAircrafts));
        OnPropertyChanged(nameof(IsTabAircraftTypes));
    }

    #region Данные коллекций справочников

    [ObservableProperty]
    private ObservableCollection<AirportDto> _airports = [];

    [ObservableProperty]
    private ObservableCollection<AircraftDto> _aircraftList = [];

    [ObservableProperty]
    private ObservableCollection<AircraftTypeDto> _aircraftTypes = [];

    #endregion

    #region Форма добавления нового аэропорта

    [ObservableProperty]
    private string _newAirportCode = string.Empty;

    [ObservableProperty]
    private string _newAirportCity = string.Empty;

    [ObservableProperty]
    private string _newAirportName = string.Empty;

    #endregion

    #region Состояние и сообщения

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DirectoriesViewModel"/>.
    /// </summary>
    /// <param name="directoryService">Сервис управления справочниками ядра.</param>
    public DirectoriesViewModel(IDirectoryService directoryService)
    {
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));

        // Первоначальная загрузка справочников
        RefreshData();
    }

    /// <summary>
    /// Переключает активную вкладку справочника (0 = Аэропорты, 1 = Борта, 2 = Типы ВС).
    /// </summary>
    /// <param name="tabIndex">Индекс вкладки.</param>
    [RelayCommand]
    public void SelectTab(object? tabIndex)
    {
        if (tabIndex != null && int.TryParse(tabIndex.ToString(), out int index))
        {
            SelectedTab = index;
            StatusMessage = null;
        }
    }

    /// <summary>
    /// Обновляет все справочные коллекции из ядра системы.
    /// </summary>
    [RelayCommand]
    public void RefreshData()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            IReadOnlyList<AirportDto> airports = _directoryService.GetAllAirports();
            Airports = new ObservableCollection<AirportDto>(airports);

            IReadOnlyList<AircraftDto> aircrafts = _directoryService.GetAllAircrafts();
            AircraftList = new ObservableCollection<AircraftDto>(aircrafts);

            IReadOnlyList<AircraftTypeDto> types = _directoryService.GetAllAircraftTypes();
            AircraftTypes = new ObservableCollection<AircraftTypeDto>(types);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки справочников: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Добавляет новый аэропорт в систему.
    /// </summary>
    [RelayCommand]
    public void AddAirport()
    {
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewAirportCode) ||
            string.IsNullOrWhiteSpace(NewAirportCity) ||
            string.IsNullOrWhiteSpace(NewAirportName))
        {
            StatusMessage = "Заполните IATA-код (3 буквы), город и название аэропорта.";
            return;
        }

        var dto = new AirportDto
        {
            Code = NewAirportCode.Trim().ToUpperInvariant(),
            City = NewAirportCity.Trim(),
            Name = NewAirportName.Trim(),
            IsActive = true
        };

        Result result = _directoryService.AddAirport(dto);
        if (result.IsFailure)
        {
            StatusMessage = $"Не удалось добавить аэропорт: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Аэропорт «{dto.City} ({dto.Code})» успешно добавлен в справочник!";
        NewAirportCode = string.Empty;
        NewAirportCity = string.Empty;
        NewAirportName = string.Empty;

        // Обновляем список аэропортов
        Airports = new ObservableCollection<AirportDto>(_directoryService.GetAllAirports());
    }

    /// <summary>
    /// Деактивирует выбранный аэропорт с защитой ядра от удаления активных узлов маршрутов.
    /// </summary>
    /// <param name="airport">Деактивируемый аэропорт.</param>
    [RelayCommand]
    public void DeactivateAirport(AirportDto? airport)
    {
        if (airport == null)
        {
            return;
        }

        StatusMessage = null;
        Result result = _directoryService.DeactivateAirport(airport.Id);

        if (result.IsFailure)
        {
            StatusMessage = $"Запрет деактивации: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Аэропорт «{airport.City} ({airport.Code})» успешно деактивирован.";
        Airports = new ObservableCollection<AirportDto>(_directoryService.GetAllAirports());
    }

    /// <summary>
    /// Переключает статус воздушного судна (В строю ↔ Техобслуживание ТО).
    /// </summary>
    /// <param name="aircraft">Выбранный борт.</param>
    [RelayCommand]
    public void ToggleAircraftStatus(AircraftDto? aircraft)
    {
        if (aircraft == null)
        {
            return;
        }

        StatusMessage = null;
        AircraftStatus nextStatus = aircraft.Status == AircraftStatus.InService
            ? AircraftStatus.Maintenance
            : AircraftStatus.InService;

        Result result = _directoryService.SetAircraftStatus(aircraft.Id, nextStatus);
        if (result.IsFailure)
        {
            StatusMessage = $"Ошибка смены статуса борта: {result.ErrorMessage}";
            return;
        }

        string statusName = nextStatus == AircraftStatus.InService ? "Введён в строй" : "Переведён на ТО";
        StatusMessage = $"Борт {aircraft.TailNumber} успешно {statusName}.";
        AircraftList = new ObservableCollection<AircraftDto>(_directoryService.GetAllAircrafts());
    }
}
