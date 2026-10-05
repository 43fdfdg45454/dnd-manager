using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Files;

/// <summary>
/// Opens a stored file: campaign files are for members of the campaign (404 for anyone else),
/// global files (library, app releases) for any authenticated user.
/// </summary>
public sealed class GetFileHandler(IFileRepository files, ICampaignAccess access, IFileStorage storage)
{
    public async Task<FileDownload> HandleAsync(Guid currentUserId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = (await files.ListByIdsAsync([fileId], cancellationToken)).SingleOrDefault() ?? throw FileErrors.FileNotFound();
        if (file.CampaignId is { } campaignId && await access.GetRoleAsync(campaignId, currentUserId, cancellationToken) is null)
        {
            throw FileErrors.FileNotFound();
        }

        var content = storage.OpenRead(file.StoragePath) ?? throw FileErrors.FileNotFound();
        return new FileDownload(content, file.ContentType, file.FileName, $"\"{file.Sha256}\"", file.CreatedAt);
    }
}
