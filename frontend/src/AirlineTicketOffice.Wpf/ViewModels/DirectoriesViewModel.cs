using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Contracts.Tariffs;
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
    private readonly ISessionService _sessionService;
    private readonly ITariffCalculator _tariffCalculator;

    /// <summary>
    /// Количество кресел бизнес-класса в одном ряду (компоновка 2+2).
    /// </summary>
    private const int BusinessSeatsPerRow = 4;

    /// <summary>
    /// Количество кресел эконом-класса в одном ряду (компоновка 3+3).
    /// </summary>
    private const int EconomySeatsPerRow = 6;

    [ObservableProperty]
    private int _selectedTab;

    /// <summary>
    /// Признак наличия у текущей сессии права на управление справочниками.
    /// </summary>
    [ObservableProperty]
    private bool _canManageDirectories;

    public bool IsTabAirports => SelectedTab == 0;
    public bool IsTabAircrafts => SelectedTab == 1;
    public bool IsTabAircraftTypes => SelectedTab == 2;
    public bool IsTabTariffs => SelectedTab == 3;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTabAirports));
        OnPropertyChanged(nameof(IsTabAircrafts));
        OnPropertyChanged(nameof(IsTabAircraftTypes));
        OnPropertyChanged(nameof(IsTabTariffs));
    }

    /// <summary>
    /// Обновляет признак режима только для чтения при смене прав сессии.
    /// </summary>
    partial void OnCanManageDirectoriesChanged(bool value) => OnPropertyChanged(nameof(IsReadOnlyMode));

    #region Данные коллекций справочников

    [ObservableProperty]
    private ObservableCollection<AirportDto> _airports = [];

    [ObservableProperty]
    private ObservableCollection<AircraftDto> _aircraftList = [];

    [ObservableProperty]
    private ObservableCollection<AircraftTypeDto> _aircraftTypes = [];

    /// <summary>
    /// Справочная тарифная сетка направлений (только чтение в W-11; редактирование — W-13).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TariffDto> _tariffs = [];

    #endregion

    #region Форма добавления нового аэропорта

    [ObservableProperty]
    private string _newAirportCode = string.Empty;

    [ObservableProperty]
    private string _newAirportCity = string.Empty;

    [ObservableProperty]
    private string _newAirportName = string.Empty;

    #endregion

    #region Форма добавления нового типа воздушного судна

    [ObservableProperty]
    private string _newAircraftTypeName = string.Empty;

    [ObservableProperty]
    private int _newAircraftTypeBusinessRows;

    [ObservableProperty]
    private int _newAircraftTypeEconomyRows;

    /// <summary>
    /// Расчётная пассажировместимость нового типа ВС:
    /// бизнес — 4 кресла в ряду, эконом — 6 кресел в ряду.
    /// </summary>
    public int NewAircraftTypeTotalSeats =>
        (NewAircraftTypeBusinessRows * BusinessSeatsPerRow) + (NewAircraftTypeEconomyRows * EconomySeatsPerRow);

    partial void OnNewAircraftTypeBusinessRowsChanged(int value) => OnPropertyChanged(nameof(NewAircraftTypeTotalSeats));

    partial void OnNewAircraftTypeEconomyRowsChanged(int value) => OnPropertyChanged(nameof(NewAircraftTypeTotalSeats));

    #endregion

    #region Форма добавления нового борта

    [ObservableProperty]
    private string _newAircraftTailNumber = string.Empty;

    [ObservableProperty]
    private AircraftTypeDto? _newAircraftType;

    #endregion

    #region Состояние и сообщения

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Признак режима только для чтения: у сессии нет права <see cref="SessionPermissions.ManageDirectories"/>.
    /// </summary>
    public bool IsReadOnlyMode => !CanManageDirectories;

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DirectoriesViewModel"/>.
    /// </summary>
    /// <param name="directoryService">Сервис управления справочниками ядра.</param>
    /// <param name="sessionService">Сервис текущей сессии для проверки прав администратора.</param>
    /// <param name="tariffCalculator">Калькулятор тарифов для справочной тарифной сетки W-11.</param>
    public DirectoriesViewModel(
        IDirectoryService directoryService,
        ISessionService sessionService,
        ITariffCalculator tariffCalculator)
    {
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _tariffCalculator = tariffCalculator ?? throw new ArgumentNullException(nameof(tariffCalculator));

        CanManageDirectories = _sessionService.HasPermission(SessionPermissions.ManageDirectories);

        // Первоначальная загрузка справочников
        RefreshData();
    }

    /// <summary>
    /// Переключает активную вкладку справочника
    /// (0 = Аэропорты, 1 = Борта, 2 = Типы ВС, 3 = Тарифы и классы).
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

            IReadOnlyList<TariffDto> tariffs = _tariffCalculator.GetAllTariffs();
            Tariffs = new ObservableCollection<TariffDto>(tariffs);
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
        if (!EnsureManagePermission("добавление аэропорта"))
        {
            return;
        }

        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewAirportCode) ||
            string.IsNullOrWhiteSpace(NewAirportCity) ||
            string.IsNullOrWhiteSpace(NewAirportName))
        {
            StatusMessage = "Заполните IATA-код (3 буквы), город и название аэропорта.";
            return;
        }

        string code = NewAirportCode.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetter))
        {
            StatusMessage = "IATA-код аэропорта должен состоять ровно из 3 латинских букв (например, SVO).";
            return;
        }

        var dto = new AirportDto
        {
            Code = code,
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

        if (!EnsureManagePermission("деактивация аэропорта"))
        {
            return;
        }

        if (!airport.IsActive)
        {
            StatusMessage = $"Аэропорт «{airport.City} ({airport.Code})» уже деактивирован.";
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

        if (!EnsureManagePermission("смена статуса воздушного судна"))
        {
            return;
        }

        if (!aircraft.IsActive)
        {
            StatusMessage = $"Борт {aircraft.TailNumber} выведен из парка и не может менять эксплуатационный статус.";
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

        string statusName = nextStatus == AircraftStatus.InService
            ? "введён в эксплуатацию"
            : "переведён на техническое обслуживание";
        StatusMessage = $"Борт {aircraft.TailNumber} {statusName}.";
        AircraftList = new ObservableCollection<AircraftDto>(_directoryService.GetAllAircrafts());
    }

    /// <summary>
    /// Добавляет новый тип воздушного судна с автоматическим расчётом пассажировместимости
    /// по компоновке рядов (бизнес 2+2, эконом 3+3).
    /// </summary>
    [RelayCommand]
    public void AddAircraftType()
    {
        if (!EnsureManagePermission("добавление типа воздушного судна"))
        {
            return;
        }

        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewAircraftTypeName))
        {
            StatusMessage = "Укажите модель типа воздушного судна (например, Airbus A320-200).";
            return;
        }

        if (NewAircraftTypeBusinessRows < 0 || NewAircraftTypeEconomyRows < 0)
        {
            StatusMessage = "Количество рядов бизнес- и эконом-класса не может быть отрицательным.";
            return;
        }

        if (NewAircraftTypeTotalSeats <= 0)
        {
            StatusMessage = "Тип ВС должен содержать хотя бы один ряд пассажирских кресел.";
            return;
        }

        var dto = new AircraftTypeDto
        {
            Name = NewAircraftTypeName.Trim(),
            BusinessRowsCount = NewAircraftTypeBusinessRows,
            EconomyRowsCount = NewAircraftTypeEconomyRows,
            TotalSeats = NewAircraftTypeTotalSeats,
            IsActive = true
        };

        Result result = _directoryService.AddAircraftType(dto);
        if (result.IsFailure)
        {
            StatusMessage = $"Не удалось добавить тип ВС: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Тип ВС «{dto.Name}» на {dto.TotalSeats} мест успешно добавлен в справочник.";
        NewAircraftTypeName = string.Empty;
        NewAircraftTypeBusinessRows = 0;
        NewAircraftTypeEconomyRows = 0;

        AircraftTypes = new ObservableCollection<AircraftTypeDto>(_directoryService.GetAllAircraftTypes());
    }

    /// <summary>
    /// Добавляет новый борт в парк авиакомпании со статусом «В эксплуатации».
    /// </summary>
    [RelayCommand]
    public void AddAircraft()
    {
        if (!EnsureManagePermission("добавление воздушного судна в парк"))
        {
            return;
        }

        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewAircraftTailNumber))
        {
            StatusMessage = "Укажите бортовой номер воздушного судна (например, RA-89014).";
            return;
        }

        if (NewAircraftType == null)
        {
            StatusMessage = "Выберите тип воздушного судна из справочника.";
            return;
        }

        var dto = new AircraftDto
        {
            TailNumber = NewAircraftTailNumber.Trim().ToUpperInvariant(),
            AircraftTypeId = NewAircraftType.Id,
            AircraftTypeName = NewAircraftType.Name,
            Status = AircraftStatus.InService,
            IsActive = true
        };

        Result result = _directoryService.AddAircraft(dto);
        if (result.IsFailure)
        {
            StatusMessage = $"Не удалось добавить борт: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Борт {dto.TailNumber} ({dto.AircraftTypeName}) успешно добавлен в парк авиакомпании.";
        NewAircraftTailNumber = string.Empty;
        NewAircraftType = null;

        AircraftList = new ObservableCollection<AircraftDto>(_directoryService.GetAllAircrafts());
    }

    /// <summary>
    /// Проверяет право текущей сессии на управление справочниками.
    /// </summary>
    /// <param name="actionDescription">Описание действия для сообщения об отказе.</param>
    /// <returns><c>true</c>, если действие разрешено; иначе <c>false</c>.</returns>
    private bool EnsureManagePermission(string actionDescription)
    {
        if (CanManageDirectories)
        {
            return true;
        }

        StatusMessage = $"Недостаточно прав: {actionDescription} доступно только администратору.";
        return false;
    }
}
