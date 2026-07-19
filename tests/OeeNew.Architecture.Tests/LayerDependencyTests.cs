using NetArchTest.Rules;
using Xunit;

namespace OeeNew.Architecture.Tests;

/// <summary>
/// Enforces AD-1 (Architecture Spine): Domain has no outward dependency,
/// Application only depends on Domain (+ its own interfaces), never directly on Infrastructure or Api.
/// </summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "OeeNew.Domain";
    private const string ApplicationNamespace = "OeeNew.Application";
    private const string InfrastructureNamespace = "OeeNew.Infrastructure";
    private const string ApiNamespace = "OeeNew.Api";

    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(OeeNew.Domain.AssemblyMarker).Assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(OeeNew.Application.AssemblyMarker).Assembly)
            .That()
            .ResideInNamespace(ApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    private static string FailureMessage(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Violating types: " + string.Join(", ", result.FailingTypes ?? []);
}
