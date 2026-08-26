namespace Expressif.Syntax.Tests;

public class BuildInfrastructureTests
{
    [Test]
    public void CompanionTestAssemblyCanAccessProductionInternals()
    {
        Assert.That(typeof(AssemblyMarker).Assembly.GetName().Name, Is.EqualTo("Expressif.Syntax"));
    }

    [Test]
    public void ProductionAssemblyExposesInternalsToTestingAssemblies()
    {
        var friendAssemblies = typeof(AssemblyMarker).Assembly
            .GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute),
                inherit: false)
            .Cast<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName);

        Assert.That(
            friendAssemblies,
            Is.EquivalentTo(["Expressif.Syntax.Tests", "Expressif.Testing"]));
    }
}
