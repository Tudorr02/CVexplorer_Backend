namespace CVexplorer.Services.Implementation;

using CVexplorer.Models.DTO;
using CVexplorer.Services.Interface;
using System.Text.Json;
using AutoMapper;
using CVexplorer.Models.Domain;
using System.Text.Json.Serialization;

public class CvEvaluationService(HttpClient http, IMapper mapper) : ICvEvaluationService
{
    private bool IsLikelyCv(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 100)
            return false;

        var keywords = new[] { "education", "experience", "skills", "summary", "objective" };
        int matchCount = keywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        return matchCount >= 1;
    }
    public async Task<CvEvaluationResultDTO> EvaluateAsync(string cvText, Position position, CancellationToken ct = default)
    {
        if (!IsLikelyCv(cvText))
        {
            throw new InvalidOperationException("Document does not appear to be a CV.");
        }

        var payload = new CvEvaluationRequestDTO
        {
            CvText = cvText,
            Position = mapper.Map<PositionPayloadInputDTO>(position)
        };

        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

      

        using var response = await http.PostAsJsonAsync("/evaluate-cv", payload, opts, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CvEvaluationResultDTO>(opts, ct);
        if (result is null)
            throw new InvalidOperationException($"Received null response when calling the Evaluation Service");

        return result;

        



       
    }

    public async Task<List<CvEvaluationResultDTO>> BulkEvaluateAsync(List<string> cvsText, Position position, CancellationToken ct = default)
    {
        var validCvTexts = cvsText.Where(text => IsLikelyCv(text)).ToList();

        var payload = new
        {
            cvTexts = validCvTexts,
            position = mapper.Map<PositionPayloadInputDTO>(position)
        };
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

       

        using var response = await http.PostAsJsonAsync("/evaluate-cvs", payload, opts, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<CvEvaluationResultDTO>>(opts, ct);

        if (result is null)
            throw new InvalidOperationException($"Received null response when calling the Evaluation Service");

        return result;

    }
}
