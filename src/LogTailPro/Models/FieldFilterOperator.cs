namespace LogTailPro.Models;

/// <summary>Comparison operators for structured typed field filtering.</summary>
public enum FieldFilterOperator
{
    /// <summary>Field value equals the target (exact string or equal number/bool).</summary>
    Equals,

    /// <summary>Field value does not equal the target.</summary>
    NotEquals,

    /// <summary>Field numeric value is strictly greater than the target.</summary>
    GreaterThan,

    /// <summary>Field numeric value is greater than or equal to the target.</summary>
    GreaterThanOrEqual,

    /// <summary>Field numeric value is strictly less than the target.</summary>
    LessThan,

    /// <summary>Field numeric value is less than or equal to the target.</summary>
    LessThanOrEqual,

    /// <summary>Field exists and is not null.</summary>
    Exists,

    /// <summary>Field is missing or null.</summary>
    NotExists,

    /// <summary>Field string representation contains the target substring.</summary>
    Contains
}
