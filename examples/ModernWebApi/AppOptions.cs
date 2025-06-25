using System.ComponentModel.DataAnnotations;

/// <summary>
///     Just some demo options.
/// </summary>
/// <remarks>
///     We use this class to bind configuration and demonstrate some configuration/ptions features.
/// </remarks>
public sealed class AppOptions
{
    /// <summary>
    ///     Name in the configuration.
    /// </summary>
    public const String SectionName = "AppOptions";

    /// <summary>
    ///     An imaginary file.
    /// </summary>
    [MaxLength( 100 )]
    public required String InputFile { get; set; }

    /// <summary>
    /// Use this to test validation.
    /// </summary>
    [MinLength( 3 )]
    public required String MinLengthString { get; set; }
}