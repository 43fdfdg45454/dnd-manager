using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Characters;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Files;

namespace Dnd.Application.Files;

/// <param name="Content">Seekable stream with the whole upload.</param>
/// <param name="Kind">Enum name of <see cref="FileKind"/>.</param>
/// <param name="CharacterId">Required for portraits.</param>
public sealed record UploadFileCommand(Stream Content, string FileName, string? Kind, Guid? CampaignId, Guid? CharacterId, bool IsAdmin);

/// <summary>
/// Stores an uploaded file. Order of checks: kind (400), permission (403/404), size (413), then the
/// content itself (400): the type is decided by the bytes and must be allowed for the kind, and
/// images must have a readable size.
/// </summary>
public sealed class UploadFileHandler(
    ICampaignAccess access,
    ICharacterRepository characters,
    IFileRepository files,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<StoredFileDto> HandleAsync(Guid currentUserId, UploadFileCommand command, CancellationToken cancellationToken = default)
    {
        if (!EnumNames.TryParse<FileKind>(command.Kind, out var kind))
        {
            throw AppException.Validation("kind", $"El tipo de fichero debe ser {EnumNames.Describe<FileKind>()}.");
        }

        var campaignId = await AuthorizeAsync(currentUserId, kind, command, cancellationToken);

        if (command.Content.Length > storage.MaxUploadBytes)
        {
            throw AppException.PayloadTooLarge($"El fichero supera el tamaño máximo permitido ({FormatMegabytes(storage.MaxUploadBytes)} MB).");
        }

        if (command.Content.Length == 0)
        {
            throw AppException.Validation("file", "El fichero está vacío.");
        }

        var detected = FileContent.Detect(command.Content);
        var contentType = FileContent.ContentTypeFor(kind, detected)
            ?? throw AppException.Validation("file", FileContent.NotAllowedMessage(kind));

        ImageSize? size = null;
        if (contentType.StartsWith("image/", StringComparison.Ordinal))
        {
            size = ImageHeaderReader.Read(command.Content, detected!.Value)
                ?? throw AppException.Validation("file", "La imagen no es válida o está dañada.");
        }

        var now = clock.UtcNow;
        var id = Guid.NewGuid();
        var saved = await storage.SaveAsync(id, FileContent.ExtensionFor(contentType), command.Content, now, cancellationToken);
        try
        {
            var file = StoredFile.Create(
                id,
                campaignId,
                currentUserId,
                CleanFileName(command.FileName, FileContent.ExtensionFor(contentType)),
                contentType,
                saved.SizeBytes,
                saved.Sha256,
                kind,
                saved.StoragePath,
                size?.Width,
                size?.Height,
                now);
            files.Add(file);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return StoredFileDto.From(file);
        }
        catch
        {
            await storage.DeleteAsync(saved.StoragePath, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Checks the actor may upload this kind and returns the campaign the file belongs to (null for global files).</summary>
    private async Task<Guid?> AuthorizeAsync(Guid currentUserId, FileKind kind, UploadFileCommand command, CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case FileKind.MapImage:
            case FileKind.LoreAttachment:
                var campaignId = command.CampaignId ?? throw AppException.Validation("campaignId", "Indica la campaña del fichero.");
                await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
                return campaignId;

            case FileKind.Portrait:
                var characterId = command.CharacterId ?? throw AppException.Validation("characterId", "Indica el personaje del retrato.");
                var character = await characters.GetOwnershipAsync(characterId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
                var role = await access.GetRoleAsync(character.CampaignId, currentUserId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
                if (command.CampaignId is { } declared && declared != character.CampaignId)
                {
                    throw AppException.Validation("campaignId", "El personaje no pertenece a esa campaña.");
                }

                if (!role.IsAtLeast(CampaignRole.DM) && character.OwnerUserId != currentUserId)
                {
                    throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden cambiar su retrato.");
                }

                return character.CampaignId;

            default:
                return command.IsAdmin ? null : throw AppException.Forbidden("Solo un administrador puede subir este tipo de fichero.");
        }
    }

    private static string FormatMegabytes(long bytes) => (bytes / (1024d * 1024d)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The base name without directories or control characters, at most 255 characters; never empty.</summary>
    private static string CleanFileName(string? fileName, string extension)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length > StoredFile.FileNameMaxLength)
        {
            name = name[^StoredFile.FileNameMaxLength..];
        }

        return name.Length == 0 ? "fichero" + extension : name;
    }
}
