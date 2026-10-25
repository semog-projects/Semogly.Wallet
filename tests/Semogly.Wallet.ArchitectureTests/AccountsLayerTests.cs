using System.Reflection;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.ArchitectureTests;

/// <summary>
/// Garante a regra de dependência da Clean Architecture no serviço Accounts:
/// as dependências apontam sempre para dentro (Api → Infrastructure → Application → Domain).
/// </summary>
public class AccountsLayerTests
{
    private const string Prefix = "Semogly.Wallet.Accounts";

    private static readonly Assembly DomainAssembly = Assembly.Load($"{Prefix}.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load($"{Prefix}.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load($"{Prefix}.Infrastructure");

    // Bibliotecas de infraestrutura que não podem vazar para as camadas internas
    private static readonly string[] InfrastructureLibraries =
    [
        "Microsoft.EntityFrameworkCore",
        "MassTransit",
        "StackExchange.Redis",
        "Microsoft.AspNetCore",
        "Npgsql",
    ];

    [Fact]
    public void Domain_ShouldNotDependOnOtherLayers() =>
        AssertNoDependency(DomainAssembly, $"{Prefix}.Application", $"{Prefix}.Infrastructure", $"{Prefix}.Api");

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructureLibraries() =>
        AssertNoDependency(DomainAssembly, InfrastructureLibraries);

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi() =>
        AssertNoDependency(ApplicationAssembly, $"{Prefix}.Infrastructure", $"{Prefix}.Api");

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureLibraries() =>
        AssertNoDependency(ApplicationAssembly, InfrastructureLibraries);

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi() =>
        AssertNoDependency(InfrastructureAssembly, $"{Prefix}.Api");

    [Fact]
    public void Handlers_ShouldBeInternalAndSealed()
    {
        Type[] handlerInterfaces = [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

        var invalidHandlers = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            .Where(t => t.IsPublic || !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(invalidHandlers);
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbiddenPrefixes)
    {
        var violations = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"{assembly.GetName().Name} não deveria depender de: {string.Join(", ", violations)}");
    }
}
