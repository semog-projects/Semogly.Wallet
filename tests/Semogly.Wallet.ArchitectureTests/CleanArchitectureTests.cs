using System.Reflection;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.ArchitectureTests;

/// <summary>
/// Garante a regra de dependência da Clean Architecture em todos os serviços:
/// as dependências apontam sempre para dentro (Api → Infrastructure → Application → Domain).
/// </summary>
public class CleanArchitectureTests
{
    // Bibliotecas de infraestrutura que não podem vazar para as camadas internas
    private static readonly string[] InfrastructureLibraries =
    [
        "Microsoft.EntityFrameworkCore",
        "MassTransit",
        "StackExchange.Redis",
        "Microsoft.AspNetCore",
        "Npgsql",
        "Marten",
        "JasperFx",
    ];

    public static TheoryData<string> Services => ["Accounts", "Transfers", "Ledger"];

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_ShouldNotDependOnOtherLayers(string service) =>
        AssertNoDependency(Layer(service, "Domain"), Name(service, "Application"), Name(service, "Infrastructure"), Name(service, "Api"));

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_ShouldNotDependOnInfrastructureLibraries(string service) =>
        AssertNoDependency(Layer(service, "Domain"), InfrastructureLibraries);

    [Theory]
    [MemberData(nameof(Services))]
    public void Application_ShouldNotDependOnInfrastructureOrApi(string service) =>
        AssertNoDependency(Layer(service, "Application"), Name(service, "Infrastructure"), Name(service, "Api"));

    [Theory]
    [MemberData(nameof(Services))]
    public void Application_ShouldNotDependOnInfrastructureLibraries(string service) =>
        AssertNoDependency(Layer(service, "Application"), InfrastructureLibraries);

    [Theory]
    [MemberData(nameof(Services))]
    public void Infrastructure_ShouldNotDependOnApi(string service) =>
        AssertNoDependency(Layer(service, "Infrastructure"), Name(service, "Api"));

    [Theory]
    [MemberData(nameof(Services))]
    public void Handlers_ShouldBeInternalAndSealed(string service)
    {
        Type[] handlerInterfaces = [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

        var invalidHandlers = Layer(service, "Application").GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            .Where(t => t.IsPublic || !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(invalidHandlers);
    }

    private static string Name(string service, string layer) => $"Semogly.Wallet.{service}.{layer}";

    private static Assembly Layer(string service, string layer) => Assembly.Load(Name(service, layer));

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
