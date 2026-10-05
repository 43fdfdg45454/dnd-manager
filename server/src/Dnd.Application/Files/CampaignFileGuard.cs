using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Files;

namespace Dnd.Application.Files;

/// <summary>Checks that a file id sent by a client points at a suitable file of the campaign (400 otherwise).</summary>
public sealed class CampaignFileGuard(IFileRepository files)
{
    /// <summary>The file must exist, belong to the campaign, have the given kind and, when asked, be an image.</summary>
    public async Task<StoredFile> RequireAsync(Guid campaignId, Guid fileId, FileKind kind, string field, string message, bool imageOnly, CancellationToken cancellationToken)
    {
        var file = (await files.ListByIdsAsync([fileId], cancellationToken)).SingleOrDefault();
        if (file is null || file.CampaignId != campaignId || file.Kind != kind || (imageOnly && !file.IsImage))
        {
            throw AppException.Validation(field, message);
        }

        return file;
    }
}
