using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface ICVEvaluationRepository
    {
        Task<CvEvaluationResult> CreateAsync(string cvText, Position position);
        Task<CvEvaluationResultDTO> UpdateAsync(Guid cvPublicId, CvEvaluationResultDTO editDto);
    }
}
