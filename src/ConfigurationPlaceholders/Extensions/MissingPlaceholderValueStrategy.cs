namespace Microsoft.Extensions.Configuration;

/// <summary>
///     How to handle missing placeholders.
/// </summary>
public enum MissingPlaceholderValueStrategy
{
    /// <summary>
    ///     Check for missing values at startup. Throw if no value is provided for placeholder.
    /// </summary>
    VerifyAllAtStartup = 0,

    /// <summary>
    ///     Throw if no value is not provided for placeholder when resolving placeholder value.
    /// </summary>
    Throw = 1,

    /// <summary>
    ///     Use empty string as value.
    /// </summary>
    UseEmptyValue = 2,

    /// <summary>
    ///     Do not replace placeholder with any value. Placeholder syntax remains in the configuration value.
    /// </summary>
    IgnorePlaceholder = 3
}