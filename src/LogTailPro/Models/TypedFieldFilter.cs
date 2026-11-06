using System.Globalization;
using System.Text.RegularExpressions;

namespace LogTailPro.Models;

/// <summary>
/// A typed condition evaluated against structured log event fields and properties.
/// Handles numeric comparisons, string matches, and existence checks with defined missing/null/type-mismatch semantics.
/// </summary>
public sealed class TypedFieldFilter
{
    public string FieldName { get; init; } = string.Empty;
    public FieldFilterOperator Operator { get; init; } = FieldFilterOperator.Equals;
    public string Value { get; init; } = string.Empty;

    /// <summary>Evaluates whether the given <paramref name="line"/> matches this typed filter.</summary>
    public bool Matches(LogLine line)
    {
        StructuredLogEvent? ev = line.StructuredEvent;

        // Plain-text lines or unparsed records do not have structured fields
        if (ev == null || !ev.IsValidJson)
        {
            return Operator == FieldFilterOperator.NotExists;
        }

        bool hasProp = ev.TryGetProperty(FieldName, out object? propValue);

        switch (Operator)
        {
            case FieldFilterOperator.Exists:
                return hasProp && propValue != null;

            case FieldFilterOperator.NotExists:
                return !hasProp || propValue == null;

            case FieldFilterOperator.Equals:
                if (!hasProp || propValue == null)
                    return Value.Equals("null", StringComparison.OrdinalIgnoreCase);
                return CompareEquals(propValue, Value);

            case FieldFilterOperator.NotEquals:
                if (!hasProp || propValue == null)
                    return !Value.Equals("null", StringComparison.OrdinalIgnoreCase);
                return !CompareEquals(propValue, Value);

            case FieldFilterOperator.GreaterThan:
                if (!hasProp || propValue == null) return false;
                return CompareNumeric(propValue, Value, (a, b) => a > b);

            case FieldFilterOperator.GreaterThanOrEqual:
                if (!hasProp || propValue == null) return false;
                return CompareNumeric(propValue, Value, (a, b) => a >= b);

            case FieldFilterOperator.LessThan:
                if (!hasProp || propValue == null) return false;
                return CompareNumeric(propValue, Value, (a, b) => a < b);

            case FieldFilterOperator.LessThanOrEqual:
                if (!hasProp || propValue == null) return false;
                return CompareNumeric(propValue, Value, (a, b) => a <= b);

            case FieldFilterOperator.Contains:
                if (!hasProp || propValue == null) return false;
                return propValue.ToString()?.Contains(Value, StringComparison.OrdinalIgnoreCase) == true;

            default:
                return true;
        }
    }

    private static bool CompareEquals(object propValue, string targetStr)
    {
        // Boolean check
        if (propValue is bool b && bool.TryParse(targetStr, out bool targetBool))
            return b == targetBool;

        // Numeric check
        if (IsNumeric(propValue) && double.TryParse(targetStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double targetNum))
        {
            double propNum = Convert.ToDouble(propValue, CultureInfo.InvariantCulture);
            return Math.Abs(propNum - targetNum) < 0.0000001;
        }

        // String / default check (strip quotes from target if user typed `"value"`)
        string unquoted = targetStr.Trim('"', '\'');
        return string.Equals(propValue.ToString(), unquoted, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNumeric(object propValue, string targetStr, Func<double, double, bool> comparison)
    {
        if (!IsNumeric(propValue)) return false; // Type mismatch: non-numeric property cannot satisfy numeric comparison
        if (!double.TryParse(targetStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double targetNum))
            return false; // Target is not a valid number

        double propNum = Convert.ToDouble(propValue, CultureInfo.InvariantCulture);
        return comparison(propNum, targetNum);
    }

    private static bool IsNumeric(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static readonly Regex BinaryOpRegex = new(
        @"^([@\w\.\-]+)\s*(==|!=|>=|<=|>|<|=|contains)\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ExistsFuncRegex = new(
        @"^(!?exists)\s*\(\s*([@\w\.\-]+)\s*\)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ExistsSuffixRegex = new(
        @"^([@\w\.\-]+)\s+(!?exists)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Tries to parse a typed field expression such as:
    /// "StatusCode >= 400", "Level == Error", "exists(UserId)", "UserId !exists", "@l = Information".
    /// Returns true if successfully parsed as a typed filter.
    /// </summary>
    public static bool TryParse(string input, out TypedFieldFilter? filter)
    {
        filter = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string trimmed = input.Trim();

        // Check exists(...) function
        Match funcMatch = ExistsFuncRegex.Match(trimmed);
        if (funcMatch.Success)
        {
            bool isNegated = funcMatch.Groups[1].Value.StartsWith('!');
            filter = new TypedFieldFilter
            {
                FieldName = funcMatch.Groups[2].Value,
                Operator = isNegated ? FieldFilterOperator.NotExists : FieldFilterOperator.Exists,
                Value = string.Empty
            };
            return true;
        }

        // Check <field> exists / !exists
        Match suffixMatch = ExistsSuffixRegex.Match(trimmed);
        if (suffixMatch.Success)
        {
            bool isNegated = suffixMatch.Groups[2].Value.StartsWith('!');
            filter = new TypedFieldFilter
            {
                FieldName = suffixMatch.Groups[1].Value,
                Operator = isNegated ? FieldFilterOperator.NotExists : FieldFilterOperator.Exists,
                Value = string.Empty
            };
            return true;
        }

        // Check binary operators: ==, !=, >=, <=, >, <, =, contains
        Match binMatch = BinaryOpRegex.Match(trimmed);
        if (binMatch.Success)
        {
            string field = binMatch.Groups[1].Value;
            string opStr = binMatch.Groups[2].Value;
            string rawVal = binMatch.Groups[3].Value.Trim();

            // Strip wrapping quotes if provided
            string val = rawVal.Trim('"', '\'');

            FieldFilterOperator op = opStr.ToLowerInvariant() switch
            {
                "==" or "=" => FieldFilterOperator.Equals,
                "!=" => FieldFilterOperator.NotEquals,
                ">=" => FieldFilterOperator.GreaterThanOrEqual,
                "<=" => FieldFilterOperator.LessThanOrEqual,
                ">"  => FieldFilterOperator.GreaterThan,
                "<"  => FieldFilterOperator.LessThan,
                "contains" => FieldFilterOperator.Contains,
                _ => FieldFilterOperator.Equals
            };

            filter = new TypedFieldFilter
            {
                FieldName = field,
                Operator = op,
                Value = val
            };
            return true;
        }

        return false;
    }
}
