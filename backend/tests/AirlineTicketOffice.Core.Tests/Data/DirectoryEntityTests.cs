using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Tests.Data;

/// <summary>
/// Модульные тесты для проверки сущностей справочников M3.
/// </summary>
public class DirectoryEntityTests
{
    [Fact]
    public void CreateDirectoryEntities_DefaultAndCustomValues_SetsPropertiesAndRelationshipsCorrectly()
    {
        // Arrange
        var airport = new Airport
        {
            Id = 1,
            Code = "SVO",
            City = "Москва",
            Name = "Шереметьево"
        };

        var aircraftType = new AircraftType
        {
            Id = 10,
            Name = "Airbus A320",
            TotalSeats = 180,
            BusinessRowsCount = 3,
            EconomyRowsCount = 27,
            LayoutConfigJson = "{\"rows\": 30}"
        };

        var aircraft = new Aircraft
        {
            Id = 100,
            TailNumber = "RA-89014",
            AircraftTypeId = aircraftType.Id,
            AircraftType = aircraftType
        };

        // Act
        aircraftType.Aircrafts.Add(aircraft);

        // Assert
        // Проверка сущности Airport
        Assert.Equal(1, airport.Id);
        Assert.Equal("SVO", airport.Code);
        Assert.Equal("Москва", airport.City);
        Assert.Equal("Шереметьево", airport.Name);
        Assert.True(airport.IsActive);

        // Проверка сущности AircraftType
        Assert.Equal(10, aircraftType.Id);
        Assert.Equal("Airbus A320", aircraftType.Name);
        Assert.Equal(180, aircraftType.TotalSeats);
        Assert.Equal(3, aircraftType.BusinessRowsCount);
        Assert.Equal(27, aircraftType.EconomyRowsCount);
        Assert.Equal("{\"rows\": 30}", aircraftType.LayoutConfigJson);
        Assert.True(aircraftType.IsActive);
        Assert.Single(aircraftType.Aircrafts);
        Assert.Same(aircraft, aircraftType.Aircrafts.First());

        // Проверка сущности Aircraft
        Assert.Equal(100, aircraft.Id);
        Assert.Equal("RA-89014", aircraft.TailNumber);
        Assert.Equal(10, aircraft.AircraftTypeId);
        Assert.Same(aircraftType, aircraft.AircraftType);
        Assert.Equal(AircraftStatus.InService, aircraft.Status);
        Assert.True(aircraft.IsActive);
    }
}
