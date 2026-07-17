using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.Rag;
using DrugCompare.Application.Services.Implementations;
using DrugCompare.Application.Services.Implementations.KnowledgeBase;
using DrugCompare.Features.ChPLNavigator;
using DrugCompare.Features.EvidenceAssistant;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Infrastructure.SQLite;
using DrugCompare.Infrastructure.SQLite.KnowledgeBase;
using DrugCompare.Infrastructure.SQLite.Rag;
using DrugCompare.ViewModels;
using DrugCompare.ViewModels.Interaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DrugCompare;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            RegisterSqliteRepositories(services);
            RegisterApplicationServices(services);
            RegisterViewModels(services);
            RegisterViews(services);

            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Nie można uruchomić aplikacji. Sprawdź plik appsettings.json i bazę danych.\n\n{ex.Message}",
                "Błąd uruchamiania",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void RegisterSqliteRepositories(IServiceCollection services)
    {
        services.AddSingleton<SqliteConnectionFactory>();

        services.AddSingleton<IDrugRepository, SqliteDrugRepository>();
        services.AddSingleton<ISubstanceRepository, SqliteSubstanceRepository>();
        services.AddSingleton<IInteractionRepository, SqliteInteractionRepository>();
        services.AddSingleton<IInteractionHistoryRepository, SqliteInteractionHistoryRepository>();
        services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();
        services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
        services.AddSingleton<IChplDocumentRepository, SqliteChplDocumentRepository>();
        services.AddSingleton<IChplSectionRepository, SqliteChplSectionRepository>();
        services.AddSingleton<IKnowledgeChunkRepository, SqliteKnowledgeChunkRepository>();
        services.AddSingleton<IDatabaseStatusRepository, SqliteDatabaseStatusRepository>();
        services.AddSingleton<IDataManagementRepository, DisabledDataManagementRepository>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<DrugDataService>();

        services.AddSingleton<IDrugLookupService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<ISubstanceLookupService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<ISubstanceSynonymService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IInteractionCheckerService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IInteractionHistoryService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IPolishDrugRegistryService, PolishDrugRegistryService>();
        services.AddSingleton<IIcdCodeService, IcdCodeService>();
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<IRagRetriever, SqliteFtsRagRetriever>();
        services.AddSingleton<IKnowledgeBaseIngestionService, KnowledgeBaseIngestionService>();
        services.AddSingleton<IKnowledgeBaseStatsService, KnowledgeBaseStatsService>();
        services.AddSingleton<IDatabaseStatusService, DatabaseStatusService>();
        services.AddSingleton<IKnowledgeBaseSearchService, KnowledgeBaseSearchService>();
        services.AddSingleton<IDataManagementService, DisabledDataManagementService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<InteractionCheckerViewModel>();
        services.AddSingleton<PolishDrugRegistryViewModel>();
        services.AddSingleton<IcdLookerViewModel>();
        services.AddSingleton<ChPLNavigatorViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<EvidenceAssistantViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }
}
