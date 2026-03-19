#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed class TestApi
{
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TestApi(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient();
    }

    public async Task ResetAsync()
    {
        var res = await _http.DeleteAsync($"{_baseUrl}/api/test/reset");
        res.EnsureSuccessStatusCode();
    }

    public async Task SeedAsync(IEnumerable<TodoCreateReq> items)
    {
        var payload = new { todos = items };
        var res = await _http.PostAsync(
            $"{_baseUrl}/api/test/seed",
            new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        );
        res.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Guid id, TodoUpdateReq dto)
    {
        var res = await _http.PutAsync(
            $"{_baseUrl}/api/todos/{id}",
            new StringContent(JsonSerializer.Serialize(dto, JsonOpts), Encoding.UTF8, "application/json")
        );
        res.EnsureSuccessStatusCode();
    }

    public async Task<Guid?> TryGetIdByTitleAsync(string title)
    {
        var res = await _http.GetAsync($"{_baseUrl}/api/todos?query={Uri.EscapeDataString(title)}");
        res.EnsureSuccessStatusCode();
        using var stream = await res.Content.ReadAsStreamAsync();
        var todos = await JsonSerializer.DeserializeAsync<List<TodoDto>>(stream, JsonOpts);
        var match = todos?.Find(t => string.Equals(t.Title, title, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }
}

// DTOs (match server; enums serialized as strings)
public record TodoCreateReq(string Title, string? Priority = null, DateTime? DueDate = null, string[]? Tags = null, string? Notes = null);
public record TodoUpdateReq(string? Title = null, string? Notes = null, string? Priority = null, DateTime? DueDate = null, string[]? Tags = null);
public record TodoDto(Guid Id, string Title, bool Completed, string Priority, DateTime? DueDate, string[] Tags, string Notes);
