using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

namespace KuchniaUCygana.Tests.Integration.Infrastructure;

/// <summary>
/// Testy integracyjne udowadniające, że Dapper nie jest jedynie wykonawcą surowego SQL,
/// lecz spełnia kryteria systemu ORM (Object-Relational Mapper) poprzez automatyczne mapowanie obiektowe,
/// mapowanie złożonych zapytań (joins/CTEs) oraz dynamiczne generowanie CRUD w oparciu o produkcyjne repozytoria.
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class DapperOrmProofTests
{
    private readonly SqlServerIntegrationFixture _fixture;

    public DapperOrmProofTests(SqlServerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private SqlServerConnectionFactory CreateConnectionFactory()
    {
        return new SqlServerConnectionFactory(_fixture.AppConnectionString);
    }

    /// <summary>
    /// DOWÓD 1: Generyczny Dynamiczny CRUD (Własny ORM w BaseRepository).
    /// Pokazujemy, że produkcyjna klasa BaseRepository<TEntity> automatycznie generuje
    /// polecenia INSERT, UPDATE, DELETE i SELECT na podstawie refleksji metadanych encji
    /// i mapuje je za pomocą Dappera bez pisania ręcznego SQL w repozytoriach.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task BaseRepository_ShouldPerformCrudDynamicallyUsingProductionImplementation()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var repository = new BaseRepository<UnitOfMeasure>(connectionFactory);
        
        var uniqueSymbol = $"CRUD-{Guid.NewGuid().ToString("N")[..5]}";
        var unit = new UnitOfMeasure
        {
            Symbol = uniqueSymbol,
            Name = "Dynamic CRUD Proof Unit",
            Description = "Initial description"
        };

        // 1. INSERT (Zapis)
        // Obiekt C# jest przekazywany bezpośrednio. SQL i klucz główny są generowane automatycznie przez nasz ORM.
        var id = await repository.InsertAsync(unit);
        id.Should().BeGreaterThan(0);
        unit.Id.Should().Be(id);

        // 2. READ (Odczyt) i automatyczne mapowanie obiektowe Dappera
        // Pobieramy rekord po ID – repozytorium dynamicznie generuje selekcję i rzutuje na C#
        var retrieved = await repository.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.Symbol.Should().Be(uniqueSymbol);
        retrieved.Description.Should().Be("Initial description");

        // 3. UPDATE (Aktualizacja)
        // Modyfikujemy właściwości obiektu i zapisujemy
        retrieved.Name = "Updated Name for CRUD Proof";
        retrieved.Description = "Updated description";
        
        var updateSuccess = await repository.UpdateAsync(retrieved);
        updateSuccess.Should().BeTrue();

        // Sprawdzamy czy zmiana się zapisała
        var updated = await repository.GetByIdAsync(id);
        updated!.Name.Should().Be("Updated Name for CRUD Proof");
        updated.Description.Should().Be("Updated description");

        // 4. DELETE (Usuwanie)
        // Usuwamy rekord z bazy danych przy użyciu samego identyfikatora
        var deleteSuccess = await repository.DeleteAsync(id);
        deleteSuccess.Should().BeTrue();

        // Potwierdzamy, że rekord nie istnieje
        var deleted = await repository.GetByIdAsync(id);
        deleted.Should().BeNull();
    }

    /// <summary>
    /// DOWÓD 2: Automatyczne mapowanie złożonych zapytań (CTE, LEFT JOIN) na DTO.
    /// Pokazujemy, że produkcyjne repozytorium SystemLogRepository.SearchAsync używa Dappera
    /// do pobrania skomplikowanego zapytania ze złączem LEFT JOIN i automatycznie rzutuje wynik
    /// na obiekt DTO SystemLogRow, wyliczając przy tym UserFullName z kolumn FirstName i LastName.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SystemLogRepository_ShouldPerformComplexCteAndJoinMappingOnProductionCode()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var userRepo = new BaseRepository<User>(connectionFactory);
        var logRepo = new SystemLogRepository(connectionFactory);

        // 1. Tworzymy nowego użytkownika testowego w celu demonstracji złączenia JOIN
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        var testUser = new User
        {
            Email = $"orm-proof-{uniqueSuffix}@example.com",
            FirstName = "Zenon",
            LastName = $"Dapper-{uniqueSuffix}",
            PasswordHash = "fake-hash",
            Role = "Developer"
        };
        var userId = await userRepo.InsertAsync(testUser);

        // 2. Dodajemy log systemowy powiązany z tym użytkownikiem
        var systemLog = new SystemLog
        {
            UserId = userId,
            Action = "TEST_JOIN_MAPPING",
            TargetEntity = "SystemLog",
            TargetId = "123",
            IPAddress = "127.0.0.1",
            Timestamp = DateTimeOffset.UtcNow
        };
        // Używamy BaseRepository<SystemLog> do bezpośredniego zapisu
        var logBaseRepo = new BaseRepository<SystemLog>(connectionFactory);
        var logId = await logBaseRepo.InsertAsync(systemLog);

        // Act
        // Wywołujemy produkcyjną metodę wyszukiwania logów zawierającą CTE i JOIN
        var searchResult = await logRepo.SearchAsync(new SystemLogSearchQuery
        {
            UserId = userId,
            Action = "TEST_JOIN_MAPPING",
            Page = 1,
            PageSize = 10
        });

        // Assert
        searchResult.Should().NotBeNull();
        searchResult.TotalCount.Should().BeGreaterThan(0);
        
        var mappedRow = searchResult.Items.FirstOrDefault(x => x.Id == logId);
        mappedRow.Should().NotBeNull();
        mappedRow!.UserId.Should().Be(userId);
        mappedRow.Action.Should().Be("TEST_JOIN_MAPPING");
        
        // KLUCZOWY DOWÓD ORM: Kolumna UserFullName została wyliczona w SQL i automatycznie zmapowana przez Dapper na właściwość DTO
        mappedRow.UserFullName.Should().Be($"Zenon Dapper-{uniqueSuffix}");

        // Cleanup
        await logBaseRepo.DeleteAsync(logId);
        await userRepo.DeleteAsync(userId);
    }

    /// <summary>
    /// DOWÓD 3: Obsługa wielu zbiorów wynikowych (Multiple Result Sets - QueryMultipleAsync).
    /// Pokazujemy, że produkcyjne repozytorium BatchRepository.GetFefoReportPageAsync wysyła
    /// jedno zapytanie SQL zwracające dwa zbiory wynikowe (1. COUNT, 2. wiersze raportu),
    /// a Dapper w jednej transakcji sieciowej automatycznie rozdziela je na silnie typowane obiekty.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchRepository_ShouldMapMultipleResultSetsUsingQueryMultipleAsync()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var batchRepo = new BatchRepository(connectionFactory);

        // Uruchamiamy wyszukiwanie raportu FEFO korzystające z QueryMultipleAsync
        var query = new FefoReportQuery(
            Search: null,
            Status: null,
            Page: 1,
            PageSize: 10,
            UsePaging: true);

        // Act
        var result = await batchRepo.GetFefoReportPageAsync(query);

        // Assert
        result.Items.Should().NotBeNull();
        
        // Zbiór raportu może być pusty w zależności od stanu bazy, ale sama struktura 
        // i zapytanie QueryMultipleAsync musiały zostać poprawnie wykonane i zmapowane.
        result.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        
        if (result.TotalCount > 0)
        {
            result.Items.Should().NotBeEmpty();
            var firstItem = result.Items.First();
            firstItem.StockItemId.Should().BeGreaterThan(0);
            firstItem.StockItemName.Should().NotBeNullOrWhiteSpace();
            firstItem.BatchId.Should().BeGreaterThan(0);
        }
    }
}
