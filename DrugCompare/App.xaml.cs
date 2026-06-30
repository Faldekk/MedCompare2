using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Application.Services.Implementations;
using DrugCompare.Features.ChPLNavigator;
using DrugCompare.Features.DrugExplorer;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Infrastructure.SQLite;
using DrugCompare.ViewModels;
using DrugCompare.ViewModels.Interaction;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Infrastructure.SQLite.KnowledgeBase;
using Microsoft.Extensions.Configuration;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Implementations.KnowledgeBase;
using Microsoft.Extensions.DependencyInjection;

namespace DrugCompare;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: true,
                reloadOnChange: true)
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
        services.AddSingleton<IDrugExplorerRepository, SqliteDrugExplorerRepository>();
        services.AddSingleton<IInteractionHistoryRepository, SqliteInteractionHistoryRepository>();
        services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();
        services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
        services.AddSingleton<IKnowledgeBaseIngestionService, KnowledgeBaseIngestionService>();
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

        services.AddSingleton<IDrugExplorerService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IPolishDrugRegistryService, PolishDrugRegistryService>();
        services.AddSingleton<IIcdCodeService, IcdCodeService>();
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<IKnowledgeBaseIngestionService, KnowledgeBaseIngestionService>();

        services.AddSingleton<IDatabaseStatusService, DatabaseStatusService>();
        services.AddSingleton<IDataManagementService, DisabledDataManagementService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<InteractionCheckerViewModel>();
        services.AddSingleton<PolishDrugRegistryViewModel>();
        services.AddSingleton<IcdLookerViewModel>();
        services.AddSingleton<ChPLNavigatorViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }
}