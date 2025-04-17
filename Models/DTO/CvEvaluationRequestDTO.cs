using System.Text.Json.Serialization;

namespace CVexplorer.Models.DTO
{
    public class CvEvaluationRequestDTO
    {
        [JsonPropertyName("cv_text")]
        public string CvText { get; set; } = null!;
        [JsonPropertyName("position")]
        public PositionDTO Position { get; set; } = null!;
    }
}
