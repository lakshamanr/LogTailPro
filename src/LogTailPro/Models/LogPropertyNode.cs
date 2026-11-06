using System.Collections.ObjectModel;
using System.Text.Json;

namespace LogTailPro.Models;

/// <summary>Hierarchical node for displaying structured log event properties in a tree or inspector view.</summary>
public sealed class LogPropertyNode
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public IReadOnlyList<LogPropertyNode> Children { get; init; } = Array.Empty<LogPropertyNode>();
    public bool HasChildren => Children.Count > 0;

    /// <summary>Builds a property node hierarchy from a <see cref="StructuredLogEvent"/>.</summary>
    public static IReadOnlyList<LogPropertyNode> FromEvent(StructuredLogEvent ev)
    {
        List<LogPropertyNode> nodes = new();

        // 1. Standard top-level metadata
        if (ev.Timestamp.HasValue)
        {
            nodes.Add(new LogPropertyNode
            {
                Name = "Timestamp",
                Type = "DateTimeOffset",
                Value = ev.Timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
            });
        }

        if (!string.IsNullOrEmpty(ev.Level))
        {
            nodes.Add(new LogPropertyNode
            {
                Name = "Level",
                Type = "String",
                Value = ev.Level
            });
        }

        if (!string.IsNullOrEmpty(ev.Message))
        {
            nodes.Add(new LogPropertyNode
            {
                Name = "Message",
                Type = "String",
                Value = ev.Message
            });
        }

        if (!string.IsNullOrEmpty(ev.MessageTemplate))
        {
            nodes.Add(new LogPropertyNode
            {
                Name = "MessageTemplate",
                Type = "String",
                Value = ev.MessageTemplate
            });
        }

        if (!string.IsNullOrEmpty(ev.Exception))
        {
            nodes.Add(new LogPropertyNode
            {
                Name = "Exception",
                Type = "String",
                Value = ev.Exception
            });
        }

        // 2. Custom and nested properties
        foreach (KeyValuePair<string, object?> kvp in ev.Properties)
        {
            nodes.Add(CreateNode(kvp.Key, kvp.Value));
        }

        return nodes;
    }

    private static LogPropertyNode CreateNode(string name, object? value)
    {
        if (value == null)
        {
            return new LogPropertyNode { Name = name, Type = "Null", Value = "null" };
        }

        if (value is bool b)
        {
            return new LogPropertyNode { Name = name, Type = "Boolean", Value = b ? "true" : "false" };
        }

        if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
        {
            return new LogPropertyNode { Name = name, Type = "Integer", Value = value.ToString() ?? "0" };
        }

        if (value is float or double or decimal)
        {
            return new LogPropertyNode { Name = name, Type = "Number", Value = value.ToString() ?? "0" };
        }

        if (value is string s)
        {
            return new LogPropertyNode { Name = name, Type = "String", Value = s };
        }

        if (value is IReadOnlyDictionary<string, object?> dict)
        {
            List<LogPropertyNode> children = new();
            foreach (KeyValuePair<string, object?> child in dict)
                children.Add(CreateNode(child.Key, child.Value));

            return new LogPropertyNode
            {
                Name = name,
                Type = "Object",
                Value = $"{{{children.Count} properties}}",
                Children = children
            };
        }

        if (value is IDictionary<string, object?> mdict)
        {
            List<LogPropertyNode> children = new();
            foreach (KeyValuePair<string, object?> child in mdict)
                children.Add(CreateNode(child.Key, child.Value));

            return new LogPropertyNode
            {
                Name = name,
                Type = "Object",
                Value = $"{{{children.Count} properties}}",
                Children = children
            };
        }

        if (value is IReadOnlyList<object?> list)
        {
            List<LogPropertyNode> children = new();
            for (int i = 0; i < list.Count; i++)
                children.Add(CreateNode($"[{i}]", list[i]));

            return new LogPropertyNode
            {
                Name = name,
                Type = "Array",
                Value = $"[{children.Count} items]",
                Children = children
            };
        }

        return new LogPropertyNode
        {
            Name = name,
            Type = value.GetType().Name,
            Value = value.ToString() ?? string.Empty
        };
    }
}
