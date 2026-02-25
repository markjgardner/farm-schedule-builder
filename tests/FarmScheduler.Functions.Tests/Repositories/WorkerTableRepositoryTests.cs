using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Moq;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using System.Linq.Expressions;

namespace FarmScheduler.Functions.Tests.Repositories;

public class WorkerTableRepositoryTests
{
    private readonly Mock<TableClient> _mockTableClient;
    private readonly WorkerTableRepository _repository;

    public WorkerTableRepositoryTests()
    {
        _mockTableClient = new Mock<TableClient>();
        var mockTableServiceClient = new Mock<TableServiceClient>();
        mockTableServiceClient
            .Setup(x => x.GetTableClient("Workers"))
            .Returns(_mockTableClient.Object);

        _repository = new WorkerTableRepository(mockTableServiceClient.Object);
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveWorkers()
    {
        var entities = new List<TableEntity>
        {
            CreateWorkerEntity("w1", "Alice", "alice@farm.com", true),
            CreateWorkerEntity("w2", "Bob", "bob@farm.com", false),
            CreateWorkerEntity("w3", "Carol", "carol@farm.com", true)
        };

        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(entities));

        var result = await _repository.GetAllActiveAsync();

        result.Should().HaveCount(2);
        result.Select(w => w.Id).Should().BeEquivalentTo(new[] { "w1", "w3" });
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsEmpty_WhenNoActiveWorkers()
    {
        var entities = new List<TableEntity>
        {
            CreateWorkerEntity("w1", "Alice", "alice@farm.com", false)
        };

        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(entities));

        var result = await _repository.GetAllActiveAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllActiveAsync_MapsPropertiesCorrectly()
    {
        var entities = new List<TableEntity>
        {
            CreateWorkerEntity("w1", "Alice", "alice@farm.com", true)
        };

        _mockTableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<Expression<Func<TableEntity, bool>>>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(entities));

        var result = await _repository.GetAllActiveAsync();

        var worker = result.Single();
        worker.Id.Should().Be("w1");
        worker.DisplayName.Should().Be("Alice");
        worker.Email.Should().Be("alice@farm.com");
        worker.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsWorker_WhenExists()
    {
        var entity = CreateWorkerEntity("w1", "Alice", "alice@farm.com", true);

        _mockTableClient
            .Setup(x => x.GetEntityAsync<TableEntity>(
                "worker", "w1",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var result = await _repository.GetByIdAsync("w1");

        result.Should().NotBeNull();
        result!.Id.Should().Be("w1");
        result.DisplayName.Should().Be("Alice");
        result.Email.Should().Be("alice@farm.com");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var result = await _repository.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsArgumentException_WhenWorkerIdEmpty()
    {
        Func<Task> act = async () => await _repository.GetByIdAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsArgumentException_WhenWorkerIdNull()
    {
        Func<Task> act = async () => await _repository.GetByIdAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_Rethrows_NonNotFoundExceptions()
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "Internal Server Error"));

        Func<Task> act = async () => await _repository.GetByIdAsync("w1");

        await act.Should().ThrowAsync<RequestFailedException>();
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

        var worker = new Worker
        {
            Id = "w1",
            DisplayName = "Alice",
            Email = "alice@farm.com",
            IsActive = true
        };

        await _repository.UpsertAsync(worker);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.PartitionKey.Should().Be("worker");
        capturedEntity.RowKey.Should().Be("w1");
        capturedEntity.GetString("DisplayName").Should().Be("Alice");
        capturedEntity.GetString("Email").Should().Be("alice@farm.com");
        capturedEntity.GetBoolean("IsActive").Should().Be(true);
    }

    [Fact]
    public async Task UpsertAsync_ThrowsArgumentNullException_WhenWorkerNull()
    {
        Func<Task> act = async () => await _repository.UpsertAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static TableEntity CreateWorkerEntity(string id, string name, string email, bool isActive)
    {
        return new TableEntity("worker", id)
        {
            { "DisplayName", name },
            { "Email", email },
            { "IsActive", isActive }
        };
    }

    private static AsyncPageable<T> CreateAsyncPageable<T>(List<T> items) where T : notnull
    {
        var page = Page<T>.FromValues(items, continuationToken: null, Mock.Of<Response>());
        return AsyncPageable<T>.FromPages(new[] { page });
    }
}
