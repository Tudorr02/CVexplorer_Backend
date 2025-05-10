using System.Text.Json.Serialization;

namespace CVexplorer.Models.DTO
{
    public class GmailPushNotificationDTO
    {
      
        [JsonPropertyName("emailAddress")]
        public string EmailAddress { get; set; }

        [JsonPropertyName("historyId")]
        public long HistoryId { get; set; }
        
    }
}
