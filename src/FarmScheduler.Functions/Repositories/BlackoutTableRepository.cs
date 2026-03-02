using Azure.Data.Tables;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class BlackoutTableRepository : IBlackoutRepository
{
    private const string TableName = "Blackouts";
    private const string PartitionKey = "blackout";
    private readonly TableClient _tableClient;

    public BlackoutTableRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<IReadOnlyList<BlackoutDate>> GetAllAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var blackouts = new List<BlackoutDate>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == PartitionKey))
        {
            var blackout = MapToBlackout(entity);
            if (blackout.Date >= today)
                blackouts.Add(blackout);
        }
        return blackouts;
    }

    public async Task<IReadOnlyList<BlackoutDate>> GetForWindowAsync(DateOnly start, DateOnly end)
    {
        var startStr = start.ToString("yyyy-MM-dd");
        var endStr = end.ToString("yyyy-MM-dd");

        var blackouts = new List<BlackoutDate>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            e => e.PartitionKey == PartitionKey))
        {
            var dateStr = entity.GetString("Date") ?? entity.RowKey;
            if (string.Compare(dateStr, startStr, StringComparison.Ordinal) >= 0 &&
                string.Compare(dateStr, endStr, StringComparison.Ordinal) <= 0)
            {
                blackouts.Add(MapToBlackout(entity));
            }
        }
        return blackouts;
    }

    public async Task UpsertAsync(BlackoutDate blackout)
    {
        var id = GenerateId(blackout);
        blackout.Id = id;

        var entity = new TableEntity(PartitionKey, id)
        {
            { "Date", blackout.Date.ToString("yyyy-MM-dd") },
            { "Description", blackout.Description },
            { "Barn", blackout.Barn?.ToString() ?? string.Empty },
            { "Shift", blackout.Shift?.ToString() ?? string.Empty }
        };
        await _tableClient.UpsertEntityAsync(entity);
    }

    public async Task DeleteAsync(string id)
    {
        await _tableClient.DeleteEntityAsync(PartitionKey, id);
    }

    private static string GenerateId(BlackoutDate blackout)
    {
        var parts = blackout.Date.ToString("yyyy-MM-dd");
        if (blackout.Barn.HasValue)
            parts += $"_{blackout.Barn.Value}";
        if (blackout.Shift.HasValue)
            parts += $"_{blackout.Shift.Value}";
        return parts;
    }

    private static BlackoutDate MapToBlackout(TableEntity entity)
    {
        var dateStr = entity.GetString("Date") ?? entity.RowKey;
        var barnStr = entity.GetString("Barn");
        var shiftStr = entity.GetString("Shift");

        return new BlackoutDate
        {
            Id = entity.RowKey,
            Date = DateOnly.ParseExact(dateStr, "yyyy-MM-dd"),
            Description = entity.GetString("Description") ?? string.Empty,
            Barn = string.IsNullOrEmpty(barnStr) ? null : Enum.Parse<Barn>(barnStr),
            Shift = string.IsNullOrEmpty(shiftStr) ? null : Enum.Parse<ShiftTime>(shiftStr)
        };
    }

    public async Task DeleteExpiredAsync(DateOnly before)
    {
        var cutoff = before.ToString("yyyy-MM-dd");
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == PartitionKey))
        {
            var dateStr = entity.GetString("Date") ?? entity.RowKey;
            if (string.Compare(dateStr, cutoff, StringComparison.Ordinal) < 0)
            {
                await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
        }
    }
}
