using CVexplorer.Models.DTO;
using CVexplorer.Models.Domain;

namespace CVexplorer.Services.Interface
{
    public interface ICvEvaluationService
    {
        Task<CvEvaluationResultDTO> EvaluateAsync(string cvText,Position position,CancellationToken ct = default);
    }
}
