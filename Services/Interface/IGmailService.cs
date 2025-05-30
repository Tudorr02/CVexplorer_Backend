using CVexplorer.Models.DTO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1.Data;

namespace CVexplorer.Services.Interface
{
    public interface IGmailService
    {
        Task<List<GmailFolderListDTO>> GetLabelsAsync(UserCredential? credential, string publicPositionId, string userId);

        Task<UserCredential> GetOrRefreshTokensAsync(string userId);

        Task<List<GmailFolderListDTO>> WatchLabels(UserCredential cred,List<string> labelIds, string positionPublicId, string userId , string? roundId= null);

        Task<bool> Unsubscribe(UserCredential cred, string positionPublicId, string userId);

        Task<List<IFormFile>> GetPdfFormFilesAsync(Google.Apis.Gmail.v1.GmailService gmailSvc, string userId, string messageId, IEnumerable<MessagePart> parts, CancellationToken ct = default);

        Task ProcessHistoryAsync(long subscriptionId, CancellationToken ct);

        Task<SessionDTO> GetSessionDataAsync(string userId, string? publicId = null);

        Task Disconnect(string userId);
    }
}
