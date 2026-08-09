namespace Expressif.Syntax.Tests;

public class BuildInfrastructureTests
{
    [Test]
    public void CompanionTestAssemblyCanAccessProductionInternals()
    {
        Assert.That(typeof(AssemblyMarker).Assembly.GetName().Name, Is.EqualTo("Expressif.Syntax"));
    }
}
