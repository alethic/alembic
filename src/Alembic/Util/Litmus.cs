using System;

namespace Alembic.Util;

/// <summary>
/// Callback to be called when a test for validity succeeds or fails.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus")]
public interface Litmus
{

    /// <summary>
    /// Implementation of <see cref="Litmus"/> that throws an exception on failure.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus", "THROW")]
    static readonly Litmus Throw = new ThrowLitmus();

    /// <summary>
    /// Implementation of <see cref="Litmus"/> that returns a status code but does not throw.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus", "IGNORE")]
    static readonly Litmus Ignore = new IgnoreLitmus();

    /// <summary>
    /// Called when a test fails. Returns false or throws.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus", "fail(String, Object...)")]
    bool Fail(string? message, params object?[] args);

    /// <summary>
    /// Called when a test succeeds. Returns true.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus", "succeed()")]
    bool Succeed() => true;

    /// <summary>
    /// Checks a condition: succeeds if it is true, otherwise fails, converting the message into a string.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus", "check(boolean, String, Object...)")]
    bool Check(bool condition, string? message, params object?[] args)
        => condition ? Succeed() : Fail(message, args);

    /// <summary>
    /// Creates a Litmus that, if it fails, will use the given message and arguments.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Litmus", "withMessageArgs(String, Object...)")]
    Litmus WithMessageArgs(string? message, params object?[] args)
        => new DelegatingLitmus(this, message, args);

}

/// <summary>The <see cref="Litmus.Throw"/> implementation: throws on failure.</summary>
file sealed class ThrowLitmus : Litmus
{
    public bool Fail(string? message, params object?[] args)
    {
        if (message is null)
            throw new InvalidOperationException();

        // Render slf4j-style "{}" placeholders in order, as Calcite's MessageFormatter.arrayFormat does.
        foreach (var arg in args)
        {
            var i = message.IndexOf("{}", StringComparison.Ordinal);
            if (i < 0)
                break;

            message = message[..i] + arg + message[(i + 2)..];
        }

        throw new InvalidOperationException(message);
    }
}

/// <summary>The <see cref="Litmus.Ignore"/> implementation: returns a status code, never throws.</summary>
file sealed class IgnoreLitmus : Litmus
{
    public bool Fail(string? message, params object?[] args) => false;

    public bool Check(bool condition, string? message, params object?[] args) => condition;

    // IGNORE never throws, so don't bother remembering message and args.
    public Litmus WithMessageArgs(string? message, params object?[] args) => this;
}

/// <summary>The <see cref="Litmus.WithMessageArgs"/> result: on failure, defers to the captured message/args.</summary>
file sealed class DelegatingLitmus : Litmus
{
    readonly Litmus _delegate;
    readonly string? _message;
    readonly object?[] _args;

    public DelegatingLitmus(Litmus @delegate, string? message, object?[] args)
    {
        _delegate = @delegate;
        _message = message;
        _args = args;
    }

    public bool Fail(string? message, params object?[] args) => _delegate.Fail(_message, _args);
}
