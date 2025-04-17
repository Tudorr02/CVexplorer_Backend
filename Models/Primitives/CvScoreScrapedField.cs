using Newtonsoft.Json.Linq;

namespace CVexplorer.Models.Primitives
{
    public class CvScoreScrapedField<TScraped>
    {
        public TScraped Scraped { get; set; } = default!;
        public double Score { get; set; } = 0;
    }
}
