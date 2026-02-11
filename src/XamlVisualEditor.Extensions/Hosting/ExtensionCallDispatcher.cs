namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Executes extension calls with error containment.</summary>
public sealed class ExtensionCallDispatcher
{
    /// <summary>Executes a call and captures exceptions.</summary>
    public async Task<ExtensionCallResult> ExecuteAsync(Func<Task> call, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await call().ConfigureAwait(false);
            return ExtensionCallResult.Success();
        }
        catch (OperationCanceledException)
        {
            return ExtensionCallResult.CreateCanceled();
        }
        catch (Exception ex)
        {
            return ExtensionCallResult.Failure(ex);
        }
    }
}

/// <summary>Represents the result of an extension call.</summary>
public readonly record struct ExtensionCallResult(bool Succeeded, bool Canceled, string? Error)
{
    /// <summary>Creates a successful result.</summary>
    public static ExtensionCallResult Success() => new(true, false, null);

    /// <summary>Creates a canceled result.</summary>
    public static ExtensionCallResult CreateCanceled() => new(false, true, "Canceled");

    /// <summary>Creates a failed result with error details.</summary>
    public static ExtensionCallResult Failure(Exception exception) => new(false, false, exception.Message);
}
