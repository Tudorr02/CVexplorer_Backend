namespace CVexplorer.Models.Domain
{
    // Models/IntegrationSubscription.cs
    public class IntegrationSubscription
    {
        public long Id { get; set; }
        public int UserId { get; set; }    
        public User User { get; set; }    
        public string Provider { get; set; }    
        public string LabelId { get; set; }    

        public Guid PositionId { get; set; }    
        public string SyncToken { get; set; }    

        public string Email { get; set; }
        public DateTimeOffset ExpiresAt { get; set; } 
        public DateTimeOffset UpdatedAt { get; set; }

        public string SubscriptionName { get; set; } 

        public required int RoundId { get; set; } 
        public Round Round { get; set; }  

        public int ProcessedCVs { get; set; } = 0;    

    }

}
