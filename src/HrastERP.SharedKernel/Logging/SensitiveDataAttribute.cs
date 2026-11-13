namespace HrastERP.SharedKernel.Logging;

/// <summary>
/// Marks a MediatR request property whose value <c>LoggingBehavior</c> logs as <c>"***"</c>
/// (passwords, tokens). On a positional record, apply it as <c>[property: SensitiveData]</c> so it
/// targets the generated property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute;
