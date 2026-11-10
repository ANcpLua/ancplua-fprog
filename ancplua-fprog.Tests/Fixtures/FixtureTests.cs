using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Fixtures;

public enum FixtureScope
{
    Test,
    Class
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UsesFixtureAttribute(FixtureScope scope) : TestClassAttribute
{
    public FixtureScope Scope { get; } = scope;
}

public interface IFixture<TSelf> : IAsyncDisposable where TSelf : class, IFixture<TSelf>
{
    static abstract Task<TSelf> CreateAsync(CancellationToken cancellationToken);
}

// MSTest invokes these inherited hooks. Test authors only choose the scope and fixture type.
public abstract class FixtureTests<TFixture> where TFixture : class, IFixture<TFixture>
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<TFixture>>> ClassFixtures = new();
    private TFixture? _fixture;
    private bool _ownsFixture;

    // MSTest injects TestContext before TestInitialize; only this infrastructure handles absence.
    [UsedImplicitly(ImplicitUseKindFlags.Assign)]
    public TestContext? TestContext { get; set; }

    protected TFixture Fixture => _fixture
        ?? throw new InvalidOperationException("The fixture is outside its test lifetime.");

    protected CancellationToken CancellationToken => Context.CancellationToken;

    private TestContext Context => TestContext
        ?? throw new InvalidOperationException("MSTest has not initialized the test context.");

    [TestInitialize]
    public async Task PrepareFixtureAsync()
    {
        var scope = GetType().GetCustomAttribute<UsesFixtureAttribute>()?.Scope
            ?? throw new InvalidOperationException("Declare UsesFixture with an explicit scope.");
        _ownsFixture = scope switch
        {
            FixtureScope.Test => true,
            FixtureScope.Class => false,
            _ => throw new InvalidOperationException("Unsupported fixture scope.")
        };

        if (_ownsFixture)
        {
            _fixture = await TFixture.CreateAsync(CancellationToken).ConfigureAwait(false);
        }
        else
        {
            var creation = ClassFixtures.GetOrAdd(Context.FullyQualifiedTestClassName,
                _ => new Lazy<Task<TFixture>>(() => TFixture.CreateAsync(CancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            _fixture = await creation.Value.ConfigureAwait(false);
        }
    }

    [TestCleanup]
    public async Task ReleaseFixtureAsync()
    {
        var fixture = _fixture;
        _fixture = null;
        if (_ownsFixture && fixture is not null)
            await fixture.DisposeAsync().ConfigureAwait(false);
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ReleaseClassFixtureAsync(TestContext context)
    {
        if (ClassFixtures.TryRemove(context.FullyQualifiedTestClassName, out var creation))
        {
            var fixture = await creation.Value.ConfigureAwait(false);
            await fixture.DisposeAsync().ConfigureAwait(false);
        }
    }
}
