using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Moq;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using System.Linq.Expressions;

namespace FarmScheduler.Functions.Tests.Repositories;

public class AvailabilityTableRepositoryTests
{
    private readonly Mock<TableClient> _mockTableClient;
    private readonly AvailabilityTableRepository _repository;

    public AvailabilityTableRepositoryTests()
    {
        _mockTableClient = new Mock<TableClient>();
        var mockTableServiceClient = new Mock<TableServiceClient>();
        mockTableServiceClient
            .Setup(x => x.GetTableClient("Availability"))
            .Returns(_mockTableClient.Object);

        _repository = new AvailabilityTableRepository(mockTableServiceClient.Object);
    }

    [Fact]
    public async Task GetByWindowAsync_ReturnsAllAvailabilityForWindow()
    {
        var entities = new List<TableEntity>
        {
            CreateAvailabilityEntity("2024-01-15", "w1", "2024-01-15", "Available"),
            CreateAvailabilityEntity("2024-01-15", "w2", "2024-01-16", "MorningOnly"),
            CreateAvailabilityEntity("2024-01-15", "w1", "2024-01-17", "NotAvailable")
        };

        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(entities));

        var result = await _repository.GetByWindowAsync("2024-01-15");

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByWindowAsync_MapsPropertiesCorrectly()
    {
        var entities = new List<TableEntity>
        {
            CreateAvailabilityEntity("2024-01-15", "w1", "2024-01-15", "MorningOnly")
        };

        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(entities));

        var result = await _repository.GetByWindowAsync("2024-01-15");

        var item = result.Single();
        item.WorkerId.Should().Be("w1");
        item.Date.Should().Be(new DateOnly(2024, 1, 15));
        item.Status.Should().Be(AvailabilityStatus.MorningOnly);
    }

    [Fact]
    public async Task GetByWindowAsync_ReturnsEmpty_WhenNoData()
    {
        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(new List<TableEntity>()));

        var result = await _repository.GetByWindowAsync("2024-01-15");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByWindowAsync_ThrowsArgumentException_WhenWindowStartEmpty()
    {
        Func<Task> act = async () => await _repository.GetByWindowAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByWindowAndWorkerAsync_ReturnsAvailabilityForWorker()
    {
        var entities = new List<TableEntity>
        {
            CreateAvailabilityEntity("2024-01-15", "w1", "2024-01-15", "Available"),
            CreateAvailabilityEntity("2024-01-15", "w1", "2024-01-16", "EveningOnly")
        };

        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(entities));

        var result = await _repository.GetByWindowAndWorkerAsync("2024-01-15", "w1");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.WorkerId.Should().Be("w1"));
    }

    [Fact]
    public async Task GetByWindowAndWorkerAsync_ThrowsArgumentException_WhenWorkerIdEmpty()
    {
        Func<Task> act = async () => await _repository.GetByWindowAndWorkerAsync("2024-01-15", "");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByWindowAndWorkerAsync_ThrowsArgumentException_WhenWindowStartEmpty()
    {
        Func<Task> act = async () => await _repository.GetByWindowAndWorkerAsync("", "w1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_CallsUpsertEntityAsync_WithCorrectProperties()
    {
        TableEntity? capturedEntity = null;
        _mockTableClient
            .Setup(x => x.UpsertEntityAsync(
                It.IsAny<TableEntity>(),
                It.IsAny<TableUpdateMode>(),
                It.IsAny<CancellationToken>()))
            .Callback<TableEntity, TableUpdateMode, CancellationToken>((e, _, _) => capturedEntity = e)
            .ReturnsAsync(Mock.Of<Response>());

        var availability = new Availability
        {
            WorkerId = "w1",
            Date = new DateOnly(2024, 1, 15),
            Status = AvailabilityStatus.Available
        };

        await _repository.UpsertAsync("2024-01-15", availability);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.PartitionKey.Should().Be("2024-01-15");
        capturedEntity.RowKey.Should().Be("w1_2024-01-15");
        capturedEntity.GetString("WorkerId").Should().Be("w1");
        capturedEntity.GetString("Date").Should().Be("2024-01-15");
        capturedEntity.GetString("Status").Should().Be("Available");
    }

    [Fact]
    public async Task UpsertAsync_ThrowsArgumentException_WhenWindowStartEmpty()
    {
        var availability = new Availability { WorkerId = "w1", Date = new DateOnly(2024, 1, 15) };
        Func<Task> act = async () => await _repository.UpsertAsync("", availability);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_ThrowsArgumentNullException_WhenAvailabilityNull()
    {
        Func<Task> act = async () => await _repository.UpsertAsync("2024-01-15", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpsertBatchAsync_SubmitsSingleBatch_WhenUnder100Items()
    {
        _mockTableClient
            .Setup(x => x.SubmitTransactionAsync(
                It.IsAny<IEnumerable<TableTransactionAction>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                (IReadOnlyList<Response>)new List<Response>(),
                Mock.Of<Response>()));

        var items = Enumerable.Range(1, 3).Select(i => new Availability
        {
            WorkerId = "w1",
            Date = new DateOnly(2024, 1, i),
            Status = AvailabilityStatus.Available
        }).ToList();

        await _repository.UpsertBatchAsync("2024-01-01", items);

        _mockTableClient.Verify(x => x.SubmitTransactionAsync(
            It.IsAny<IEnumerable<TableTransactionAction>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertBatchAsync_SubmitsMultipleBatches_WhenOver100Items()
    {
        _mockTableClient
            .Setup(x => x.SubmitTransactionAsync(
                It.IsAny<IEnumerable<TableTransactionAction>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                (IReadOnlyList<Response>)new List<Response>(),
                Mock.Of<Response>()));

        var items = Enumerable.Range(1, 150).Select(i => new Availability
        {
            WorkerId = "w1",
            Date = DateOnly.FromDayNumber(i),
            Status = AvailabilityStatus.Available
        }).ToList();

        await _repository.UpsertBatchAsync("2024-01-01", items);

        _mockTableClient.Verify(x => x.SubmitTransactionAsync(
            It.IsAny<IEnumerable<TableTransactionAction>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpsertBatchAsync_HandlesEmptyList()
    {
        await _repository.UpsertBatchAsync("2024-01-01", new List<Availability>());

        _mockTableClient.Verify(x => x.SubmitTransactionAsync(
            It.IsAny<IEnumerable<TableTransactionAction>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpsertBatchAsync_ThrowsArgumentException_WhenWindowStartEmpty()
    {
        Func<Task> act = async () => await _repository.UpsertBatchAsync("", new List<Availability>());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertBatchAsync_ThrowsArgumentNullException_WhenAvailabilityNull()
    {
        Func<Task> act = async () => await _repository.UpsertBatchAsync("2024-01-01", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static TableEntity CreateAvailabilityEntity(
        string partitionKey, string workerId, string date, string status)
    {
        return new TableEntity(partitionKey, $"{workerId}_{date}")
        {
            { "WorkerId", workerId },
            { "Date", date },
            { "Status", status }
        };
    }

    private static AsyncPageable<T> CreateAsyncPageable<T>(List<T> items) where T : notnull
    {
        var page = Page<T>.FromValues(items, continuationToken: null, Mock.Of<Response>());
        return AsyncPageable<T>.FromPages(new[] { page });
    }
}
