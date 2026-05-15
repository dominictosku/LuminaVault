using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Smoke tests for the inventory CRUD plumbing (House → Room → Item). The finance
/// suite is well-covered; this brings the other half of the app under test so future
/// refactors get the same "did I break it?" signal.
public class InventoryTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public InventoryTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record HouseDto(int Id, string Name, string? Description);
    private record RoomDto(int Id, int HouseId, string Name, string Color,
        double X, double Z, double Width, double Depth, double Height);
    private record ItemDto(int Id, string Name, string? Category, int Quantity);

    [Fact]
    public async Task Create_house_then_list_returns_it()
    {
        var create = await _api.PostAsync("/api/houses/", new { name = "Home", description = "Main residence" });
        create.EnsureSuccessStatusCode();

        var houses = await _api.GetAsync<HouseDto[]>("/api/houses/");
        Assert.Contains(houses!, h => h.Name == "Home");
    }

    [Fact]
    public async Task House_room_cascade_delete_removes_rooms()
    {
        var houseResp = await _api.PostAsync("/api/houses/", new { name = "Cottage", description = (string?)null });
        var house = await houseResp.Content.ReadFromJsonAsync<HouseDto>();

        await _api.PostAsync($"/api/houses/{house!.Id}/rooms", new
        {
            name = "Kitchen", color = "#7c3aed",
            x = 0.0, z = 0.0, width = 4.0, depth = 4.0, height = 2.6,
        });

        var del = await _api.DeleteAsync($"/api/houses/{house.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Cascade-delete: removing the house should remove its rooms via the FK
        // ON DELETE CASCADE rule. The endpoint doesn't 404 on a missing house —
        // it just filters Rooms.HouseId, so we assert the result is empty instead.
        var roomsAfter = await _api.GetAsync<RoomDto[]>($"/api/houses/{house.Id}/rooms");
        Assert.Empty(roomsAfter!);
    }

    [Fact]
    public async Task Items_filter_by_room()
    {
        var house = (await (await _api.PostAsync("/api/houses/",
            new { name = "Apt", description = (string?)null })).Content
            .ReadFromJsonAsync<HouseDto>())!;
        var room = (await (await _api.PostAsync($"/api/houses/{house.Id}/rooms", new
        {
            name = "Office", color = "#7c3aed",
            x = 0.0, z = 0.0, width = 4.0, depth = 4.0, height = 2.6,
        })).Content.ReadFromJsonAsync<RoomDto>())!;

        // Item in the room
        await _api.PostAsync("/api/items/", new
        {
            name = "Monitor", category = "IT",
            description = (string?)null, brand = (string?)null, model = (string?)null,
            serialNumber = (string?)null, value = (decimal?)400m,
            purchaseDate = (DateTime?)null, warrantyUntil = (DateTime?)null,
            quantity = 1, notes = (string?)null, tags = Array.Empty<string>(),
            roomId = (int?)room.Id, furnitureId = (int?)null, containerId = (int?)null,
        });

        // Item with no room
        await _api.PostAsync("/api/items/", new
        {
            name = "Loose pen", category = "Office Supplies",
            description = (string?)null, brand = (string?)null, model = (string?)null,
            serialNumber = (string?)null, value = (decimal?)null,
            purchaseDate = (DateTime?)null, warrantyUntil = (DateTime?)null,
            quantity = 1, notes = (string?)null, tags = Array.Empty<string>(),
            roomId = (int?)null, furnitureId = (int?)null, containerId = (int?)null,
        });

        var inRoom = await _api.GetAsync<ItemDto[]>($"/api/items/?roomId={room.Id}");
        Assert.Single(inRoom!);
        Assert.Equal("Monitor", inRoom![0].Name);
    }

    [Fact]
    public async Task Delete_item_returns_no_content_and_removes_it()
    {
        var resp = await _api.PostAsync("/api/items/", new
        {
            name = "Disposable", category = (string?)null,
            description = (string?)null, brand = (string?)null, model = (string?)null,
            serialNumber = (string?)null, value = (decimal?)null,
            purchaseDate = (DateTime?)null, warrantyUntil = (DateTime?)null,
            quantity = 1, notes = (string?)null, tags = Array.Empty<string>(),
            roomId = (int?)null, furnitureId = (int?)null, containerId = (int?)null,
        });
        var item = (await resp.Content.ReadFromJsonAsync<ItemDto>())!;

        var del = await _api.DeleteAsync($"/api/items/{item.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        await _api.EnsureAuthedAsync();
        var get = await _api.Raw.GetAsync($"/api/items/{item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
