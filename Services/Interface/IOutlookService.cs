using CVexplorer.Models.DTO;
using static CVexplorer.Services.Implementation.OutlookService;

namespace CVexplorer.Services.Interface
{
    public interface IOutlookService
    {
        Task<TokenResult> GetOrRefreshTokensAsync(string userId);

        Task<List<OutlookFolderListDTO>> GetFoldersAsync (string userId , TokenResult tokens , string publicPosId);

        Task<List<OutlookFolderListDTO>> SubscribeFolders(List<string> folderIds, string userId, TokenResult tokens, string publicPosId , string? roundId = null);

        Task ProcessNewMessageAsync(string messageId, string folderId, long subscriptionId);
        Task <bool> UnsubscribeAsync( string userId, TokenResult tokens, string publicPosId);

        Task Disconnect(string userId, TokenResult tokens);

        Task<SessionDTO> GetSessionDataAsync(string userId, string? publicId = null);

    }
}
