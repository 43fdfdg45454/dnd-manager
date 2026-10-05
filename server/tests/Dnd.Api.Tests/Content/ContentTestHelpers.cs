using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dnd.Application.Files;
using Dnd.Application.Lore;
using Dnd.Application.Maps;

namespace Dnd.Api.Tests.Content;

/// <summary>Factory with a 1 MB upload limit, to exercise the size checks with small files.</summary>
public sealed class ContentApiFactory : ApiFactory
{
    public const int LimitMegabytes = 1;

    protected override int MaxUploadMegabytes => LimitMegabytes;
}

/// <summary>Byte builders and HTTP helpers shared by the file, lore, map and library tests.</summary>
internal static class ContentTestHelpers
{
    public static byte[] Png(int width = 640, int height = 480, int padding = 0)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        stream.Write([0, 0, 0, 13]);
        stream.Write("IHDR"u8);
        stream.Write(BigEndian(width));
        stream.Write(BigEndian(height));
        stream.Write([8, 2, 0, 0, 0, 0, 0, 0, 0]); // depth, color type, methods, then a dummy CRC
        stream.Write([0, 0, 0, 0]);
        stream.Write("IEND"u8);
        stream.Write([0, 0, 0, 0]);
        stream.Write(new byte[padding]);
        return stream.ToArray();
    }

    public static byte[] Jpeg(int width = 800, int height = 600)
    {
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8]);
        stream.Write([0xFF, 0xE0, 0x00, 0x10]);
        stream.Write("JFIF\0"u8);
        stream.Write([1, 1, 0, 0, 1, 0, 1, 0, 0]);
        stream.Write([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        stream.Write([(byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width]);
        stream.Write([3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1]);
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    /// <summary>Lossless WebP header (VP8L) followed by some padding.</summary>
    public static byte[] WebpLossless(int width = 320, int height = 200)
    {
        using var stream = new MemoryStream();
        stream.Write("RIFF"u8);
        stream.Write([0x20, 0, 0, 0]);
        stream.Write("WEBPVP8L"u8);
        stream.Write([0x0A, 0, 0, 0, 0x2F]);
        var bits = (uint)(width - 1) | ((uint)(height - 1) << 14);
        stream.Write(BitConverter.GetBytes(bits));
        stream.Write(new byte[16]);
        return stream.ToArray();
    }

    public static byte[] Pdf() => "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n"u8.ToArray();

    public static byte[] Apk() => [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00];

    public static async Task<HttpResponseMessage> UploadAsync(
        this HttpClient client,
        byte[] bytes,
        string kind,
        Guid? campaignId = null,
        Guid? characterId = null,
        string fileName = "file.bin",
        string contentType = "application/octet-stream")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(file, "file", fileName);
        content.Add(new StringContent(kind), "kind");
        if (campaignId is { } campaign)
        {
            content.Add(new StringContent(campaign.ToString()), "campaignId");
        }

        if (characterId is { } character)
        {
            content.Add(new StringContent(character.ToString()), "characterId");
        }

        return await client.PostAsync("/api/v1/files", content);
    }

    public static async Task<StoredFileDto> UploadOkAsync(
        this SignedInUser user,
        byte[] bytes,
        string kind,
        Guid? campaignId = null,
        Guid? characterId = null,
        string fileName = "file.bin")
    {
        var response = await user.Client.UploadAsync(bytes, kind, campaignId, characterId, fileName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StoredFileDto>())!;
    }

    public static async Task<StoredFileDto> UploadOkAsync(this HttpClient client, byte[] bytes, string kind, Guid? campaignId = null, string fileName = "file.bin")
    {
        var response = await client.UploadAsync(bytes, kind, campaignId, fileName: fileName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StoredFileDto>())!;
    }

    public static async Task<MapDto> CreateMapAsync(this SignedInUser dm, Guid campaignId, string name = "Mapa", string visibility = "Players", int width = 640, int height = 480)
    {
        var file = await dm.UploadOkAsync(Png(width, height), "MapImage", campaignId);
        var response = await dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/maps", new { name, fileId = file.Id, visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MapDto>())!;
    }

    public static async Task<MapPinDto> CreatePinAsync(this SignedInUser dm, Guid mapId, string title = "Pin", string visibility = "Players", double x = 0.25, double y = 0.75, Guid? loreEntryId = null)
    {
        var response = await dm.Client.PostAsJsonAsync($"/api/v1/maps/{mapId}/pins", new { x, y, title, note = "Nota", icon = "place", color = "#FF8800", loreEntryId, visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MapPinDto>())!;
    }

    public static async Task<LoreEntryDto> CreateLoreAsync(
        this SignedInUser dm,
        Guid campaignId,
        string title,
        string visibility = "Players",
        string category = "Place",
        Guid? parentId = null,
        string content = "Texto")
    {
        var response = await dm.Client.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/lore",
            new { title, category, contentMarkdown = content, visibility, parentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoreEntryDto>())!;
    }

    /// <summary>True when the file can still be downloaded by <paramref name="user"/>.</summary>
    public static async Task<bool> FileExistsAsync(this SignedInUser user, Guid fileId) =>
        (await user.Client.GetAsync($"/api/v1/files/{fileId}")).StatusCode == HttpStatusCode.OK;

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
