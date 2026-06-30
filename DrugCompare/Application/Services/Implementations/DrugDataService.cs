using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Services.Contracts;

namespace DrugCompare.Application.Services.Implementations;

public sealed class DrugDataService :
    IDrugLookupService,
    ISubstanceLookupService,
    ISubstanceSynonymService,
    IInteractionCheckerService,
    IInteractionHistoryService,
    IDrugExplorerService
{
    private readonly IDrugRepository _drugRepository;
    private readonly ISubstanceRepository _substanceRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IInteractionHistoryRepository _interactionHistoryRepository;
    private readonly IDrugExplorerRepository _drugExplorerRepository;

    public DrugDataService(
        IDrugRepository drugRepository,
        ISubstanceRepository substanceRepository,
        IInteractionRepository interactionRepository,
        IInteractionHistoryRepository interactionHistoryRepository,
        IDrugExplorerRepository drugExplorerRepository)
    {
        _drugRepository = drugRepository;
        _substanceRepository = substanceRepository;
        _interactionRepository = interactionRepository;
        _interactionHistoryRepository = interactionHistoryRepository;
        _drugExplorerRepository = drugExplorerRepository;
    }

    public Task<DrugLookupResult?> FindDrugAsync(string drugName)
    {
        return _drugRepository.FindDrugAsync(drugName);
    }

    public Task<ActiveSubstanceItem?> FindActiveSubstanceAsync(string substanceName)
    {
        return _substanceRepository.FindActiveSubstanceAsync(substanceName);
    }

    public Task AddSynonymAsync(
        long activeSubstanceId,
        string synonym,
        string source = "manual")
    {
        return _substanceRepository.AddSynonymAsync(
            activeSubstanceId,
            synonym,
            source);
    }

    public Task<List<ActiveSubstanceSynonymItem>> GetSynonymsAsync(
        long activeSubstanceId)
    {
        return _substanceRepository.GetSynonymsAsync(activeSubstanceId);
    }

    public Task<List<InteractionResult>> CheckInteractionsAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances)
    {
        return _interactionRepository.CheckInteractionsAsync(substances);
    }

    public Task SaveInteractionCheckAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances,
        IReadOnlyCollection<InteractionResult> results)
    {
        return _interactionHistoryRepository.SaveInteractionCheckAsync(
            substances,
            results);
    }

    public Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(
        int limit = 20)
    {
        return _interactionHistoryRepository.GetRecentHistoryAsync(limit);
    }

    public Task<List<DrugExplorerResult>> SearchAsync(
        string query,
        int limit = 50)
    {
        return _drugExplorerRepository.SearchAsync(query, limit);
    }
}