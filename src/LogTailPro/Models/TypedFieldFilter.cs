using System.Globalization;
using System.Text.RegularExpressions;

namespace LogTailPro.Models;

/// <summary>
/// A typed condition evaluated against structured log event fields and properties.
/// Handles numeric comparisons, string matches, and existence checks with defined missing/null/type-mismatch semantics.
/// </summary>
public sealed class TypedFieldFilter
{
    private string _value = string.Empty;
    private double? _parsedTargetNum;
    private bool? _parsedTargetBool;
    private string _unquotedValue = string.Empty;

    public string FieldName { get; init; } = string.Empty;
    public FieldFilterOperator Operator { get; init; } = FieldFilterOperator.Equals;
    public string Value
    {
        get => _value;
        init
        {
            _value = value;
            _unquotedValue = value.Trim('"', '\'');
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
                _parsedTargetNum = n;
            if (bool.TryParse(value, out bool b))
                _parsedTargetBool = b;
        }
    }

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
                return CompareEquals(propValue);

            case FieldFilterOperator.NotEquals:
                if (!hasProp || propValue == null)
                    return !Value.Equals("null", StringComparison.OrdinalIgnoreCase);
                return !CompareEquals(propValue);

            case FieldFilterOperator.GreaterThan:
            case FieldFilterOperator.GreaterThanOrEqual:
            case FieldFilterOperator.LessThan:
            case FieldFilterOperator.LessThanOrEqual:
                if (!hasProp || propValue == null) return false;
                return CompareNumeric(propValue, Operator);

            case FieldFilterOperator.Contains:
                if (!hasProp || propValue == null) return false;
                return propValue.ToString()?.Contains(Value, StringComparison.OrdinalIgnoreCase) == true;

            default:
                return true;
        }
    }

    private bool CompareEquals(object propValue)
    {
        // Boolean check
        if (_parsedTargetBool.HasValue && propValue is bool b)
            return b == _parsedTargetBool.Value;

        // Numeric check
        if (_parsedTargetNum.HasValue && IsNumeric(propValue))
        {
            double propNum = Convert.ToDouble(propValue, CultureInfo.InvariantCulture);
            return Math.Abs(propNum - _parsedTargetNum.Value) < 0.0000001;
        }

        // String / default check (strip quotes from target if user typed `"value"`)
        return string.Equals(propValue.ToString(), _unquotedValue, StringComparison.OrdinalIgnoreCase);
    }

    private bool CompareNumeric(object propValue, FieldFilterOperator op)
    {
        if (!_parsedTargetNum.HasValue || !IsNumeric(propValue)) return false;
        double propNum = Convert.ToDouble(propValue, CultureInfo.InvariantCulture);
        double targetNum = _parsedTargetNum.Value;
        return op switch
        {
            FieldFilterOperator.GreaterThan => propNum > targetNum,
            FieldFilterOperator.GreaterThanOrEqual => propNum >= targetNum,
            FieldFilterOperator.LessThan => propNum < targetNum,
            FieldFilterOperator.LessThanOrEqual => propNum <= targetNum,
            _ => false
        };
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
