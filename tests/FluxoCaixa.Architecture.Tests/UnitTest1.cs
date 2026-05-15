using Shouldly;
using NetArchTest.Rules;

namespace FluxoCaixa.Architecture.Tests;

public sealed class ArchitectureTests
{
    // Nomes dos assemblies ──────────────────────────────────────────────────────

    private static Types OperationsDomain =>
        Types.InAssembly(typeof(FluxoCaixa.Operations.Domain.Entities.Transaction).Assembly);

    private static Types OperationsApplication =>
        Types.InAssembly(typeof(FluxoCaixa.Operations.Application.Handlers.CreateTransactionHandler).Assembly);

    private static Types ConsolidationDomain =>
        Types.InAssembly(typeof(FluxoCaixa.Consolidation.Domain.Entities.DailyBalance).Assembly);

    private static Types ConsolidationApplication =>
        Types.InAssembly(typeof(FluxoCaixa.Consolidation.Application.Handlers.GetDailyBalanceHandler).Assembly);

    // ─── Regras de dependência ──────────────────────────────────────────────────

    [Fact]
    public void OperationsDomain_ShouldNotDependOn_Infrastructure()
    {
        var result = OperationsDomain
            .That().ResideInNamespace("FluxoCaixa.Operations.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "FluxoCaixa.Operations.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "a camada de Domínio deve ser livre de dependências de infraestrutura");
    }

    [Fact]
    public void OperationsApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = OperationsApplication
            .That().ResideInNamespace("FluxoCaixa.Operations.Application")
            .ShouldNot().HaveDependencyOnAny(
                "FluxoCaixa.Operations.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "a camada de Application não deve referenciar Infrastructure diretamente");
    }

    [Fact]
    public void ConsolidationDomain_ShouldNotDependOn_Infrastructure()
    {
        var result = ConsolidationDomain
            .That().ResideInNamespace("FluxoCaixa.Consolidation.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "FluxoCaixa.Consolidation.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "a camada de Domínio deve ser livre de dependências de infraestrutura");
    }

    [Fact]
    public void ConsolidationApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = ConsolidationApplication
            .That().ResideInNamespace("FluxoCaixa.Consolidation.Application")
            .ShouldNot().HaveDependencyOnAny(
                "FluxoCaixa.Consolidation.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "a camada de Application não deve referenciar Infrastructure diretamente");
    }

    // ─── Convenções de nomenclatura ──────────────────────────────────────────────────

    [Fact]
    public void Handlers_ShouldResideIn_HandlersNamespace()
    {
        var result = OperationsApplication
            .That().HaveNameEndingWith("Handler")
            .Should().ResideInNamespaceContaining("Handlers")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "todas as classes Handler devem estar em um namespace Handlers");
    }

    [Fact]
    public void ConsolidationHandlers_ShouldResideIn_HandlersNamespace()
    {
        var result = ConsolidationApplication
            .That().HaveNameEndingWith("Handler")
            .Should().ResideInNamespaceContaining("Handlers")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "todas as classes Handler devem estar em um namespace Handlers");
    }

    [Fact]
    public void DomainEntities_ShouldResideIn_EntitiesNamespace()
    {
        var result = OperationsDomain
            .That().HaveNameEndingWith("Transaction")
            .Or().HaveNameEndingWith("TransactionId")
            .Should().ResideInNamespaceContaining("FluxoCaixa.Operations.Domain")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
