namespace ConfigurationPlaceholders;

/// <summary>
///     Exception thrown when a value for a placeholder is missing.
/// </summary>
public sealed class ConfigurationPlaceholderMissingException( String? message ) : Exception( message )
{
}