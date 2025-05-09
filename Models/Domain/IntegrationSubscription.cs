namespace CVexplorer.Models.Domain
{
    // Models/IntegrationSubscription.cs
    public class IntegrationSubscription
    {
        public long Id { get; set; }
        public int UserId { get; set; }    // FK către AspNetUsers
        public User User { get; set; }    // FK către AspNetUsers
        public string Provider { get; set; }    // "Gmail"
        public string Resource { get; set; }    // ex. "me/label/{labelId}"
        public string SyncToken { get; set; }    // Gmail → historyId

        public string Email { get; set; }    // email-ul utilizatorului
        public DateTimeOffset ExpiresAt { get; set; }   // când expiră watch-ul
        public DateTimeOffset UpdatedAt { get; set; }
    }

}
