using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

[TestClass]
public sealed class FpDescriptorsTests
{
    [TestMethod]
    public void Create_UsesFunctionalCategoryWarningSeverityAndEnabledByDefault()
    {
        var descriptor = FpDescriptors.Create("FP0000", "Title", "Message");

        Assert.AreEqual("FP0000", descriptor.Id);
        Assert.AreEqual("Functional", descriptor.Category);
        Assert.AreEqual(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.IsTrue(descriptor.IsEnabledByDefault);
    }
}
