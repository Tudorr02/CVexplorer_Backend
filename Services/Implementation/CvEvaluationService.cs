namespace CVexplorer.Services.Implementation;

using CVexplorer.Models.DTO;
using CVexplorer.Services.Interface;
using System.Text.Json;
using AutoMapper;
using CVexplorer.Models.Domain;
using System.Text.Json.Serialization;

public class CvEvaluationService(HttpClient http, IMapper mapper) : ICvEvaluationService
{

    //private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    //private static readonly JsonSerializerOptions CaseIns = new() { PropertyNameCaseInsensitive = true };


    public async Task<CvEvaluationResultDTO> EvaluateAsync(string cvText, Position position,CancellationToken ct = default)
    {
        var payload = new CvEvaluationRequestDTO
        {
            CvText = cvText,
            Position = mapper.Map<PositionDTO>(position)
        };

        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,                 
            Converters = { new JsonStringEnumConverter() }    
        };


        using var response = await http.PostAsJsonAsync("/evaluate-cv", payload, opts, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CvEvaluationResultDTO>(opts,ct);

        if (result is null)
            result = new CvEvaluationResultDTO();

        return result;
    }
}
