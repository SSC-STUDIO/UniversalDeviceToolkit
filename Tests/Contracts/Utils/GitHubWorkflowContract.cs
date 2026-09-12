using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UniversalDeviceToolkit.Tests.Utils;

/// <summary>
/// Reads the small, stable subset of GitHub Actions YAML used by this repository.
/// The guard tests assert jobs and steps instead of depending on workflow formatting.
/// </summary>
internal sealed class GitHubWorkflowContract
{
    private readonly Dictionary<string, WorkflowJobContract> _jobs;

    private GitHubWorkflowContract(
        IReadOnlyDictionary<string, WorkflowTriggerContract> triggers,
        Dictionary<string, WorkflowJobContract> jobs)
    {
        Triggers = triggers;
        _jobs = jobs;
    }

    public IReadOnlyDictionary<string, WorkflowTriggerContract> Triggers { get; }

    public IReadOnlyCollection<WorkflowJobContract> Jobs => _jobs.Values;

    public WorkflowJobContract Job(string id) =>
        _jobs.TryGetValue(id, out var job)
            ? job
            : throw new KeyNotFoundException($"Workflow job '{id}' was not found.");

    public IEnumerable<WorkflowStepContract> Steps => _jobs.Values.SelectMany(job => job.Steps);

    public static GitHubWorkflowContract Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var lines = yaml
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select((raw, index) => SourceLine.Create(raw, index + 1))
            .ToArray();

        var triggers = ParseTriggers(lines);
        var jobs = ParseJobs(lines);
        return new GitHubWorkflowContract(triggers, jobs);
    }

    private static Dictionary<string, WorkflowTriggerContract> ParseTriggers(SourceLine[] lines)
    {
        var result = new Dictionary<string, WorkflowTriggerContract>(StringComparer.OrdinalIgnoreCase);
        var triggerIndex = FindTopLevelKey(lines, "on");
        if (triggerIndex < 0)
            return result;

        var triggerLine = ReadKeyValue(lines[triggerIndex]);
        if (!string.IsNullOrWhiteSpace(triggerLine.Value))
        {
            foreach (var trigger in Unquote(triggerLine.Value).Trim('[', ']')
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                result[Unquote(trigger)] = new WorkflowTriggerContract(Unquote(trigger), []);
            }

            return result;
        }

        var index = triggerIndex + 1;
        while (index < lines.Length)
        {
            if (lines[index].IsIgnorable)
            {
                index++;
                continue;
            }

            if (lines[index].Indent == 0)
                break;

            if (lines[index].Indent != 2)
            {
                index++;
                continue;
            }

            var trigger = ReadKeyValue(lines[index]);
            var paths = new List<string>();
            var triggerContract = new WorkflowTriggerContract(trigger.Key, paths);
            index++;

            while (index < lines.Length)
            {
                if (lines[index].IsIgnorable)
                {
                    index++;
                    continue;
                }

                if (lines[index].Indent <= 2)
                    break;

                if (lines[index].Indent == 4)
                {
                    if (lines[index].Content.StartsWith("- ", StringComparison.Ordinal))
                    {
                        triggerContract.Values.Add(lines[index].Content[2..].Trim());
                        index++;
                        continue;
                    }

                    var nested = ReadKeyValue(lines[index]);
                    if (nested.Key is "paths" or "paths-ignore")
                    {
                        index++;
                        while (index < lines.Length)
                        {
                            if (lines[index].IsIgnorable)
                            {
                                index++;
                                continue;
                            }

                            if (lines[index].Indent <= 4)
                                break;

                            if (lines[index].Indent == 6 && lines[index].Content.StartsWith("- ", StringComparison.Ordinal))
                                paths.Add(Unquote(lines[index].Content[2..].Trim()));
                            index++;
                        }

                        continue;
                    }
                }

                index++;
            }

            result[trigger.Key] = triggerContract;
        }

        return result;
    }

    private static Dictionary<string, WorkflowJobContract> ParseJobs(SourceLine[] lines)
    {
        var result = new Dictionary<string, WorkflowJobContract>(StringComparer.OrdinalIgnoreCase);
        var jobsIndex = FindTopLevelKey(lines, "jobs");
        if (jobsIndex < 0)
            return result;

        var index = jobsIndex + 1;
        while (index < lines.Length)
        {
            if (lines[index].IsIgnorable)
            {
                index++;
                continue;
            }

            if (lines[index].Indent == 0)
                break;

            if (lines[index].Indent != 2)
            {
                index++;
                continue;
            }

            var job = ReadKeyValue(lines[index]);
            index++;
            var contract = new WorkflowJobContract(job.Key);

            while (index < lines.Length)
            {
                if (lines[index].IsIgnorable)
                {
                    index++;
                    continue;
                }

                if (lines[index].Indent <= 2)
                    break;

                if (lines[index].Indent == 4)
                {
                    var property = ReadKeyValue(lines[index]);
                    if (property.Key == "runs-on")
                        contract.RunsOn = Unquote(property.Value);
                    else if (property.Key == "env")
                    {
                        index++;
                        ParseJobEnvironment(contract, lines, ref index);
                        continue;
                    }
                    else if (property.Key == "steps")
                    {
                        index++;
                        contract.Steps.AddRange(ParseSteps(lines, ref index));
                        continue;
                    }
                }

                index++;
            }

            result[job.Key] = contract;
        }

        return result;
    }

    private static void ParseJobEnvironment(WorkflowJobContract job, SourceLine[] lines, ref int index)
    {
        while (index < lines.Length)
        {
            if (lines[index].IsIgnorable)
            {
                index++;
                continue;
            }

            if (lines[index].Indent <= 4)
                break;

            if (lines[index].Indent == 6)
            {
                var property = ReadKeyValue(lines[index]);
                job.Environment[property.Key] = Unquote(property.Value);
            }

            index++;
        }
    }

    private static IReadOnlyList<WorkflowStepContract> ParseSteps(SourceLine[] lines, ref int index)
    {
        var result = new List<WorkflowStepContract>();
        var stepIndent = FindFirstContentIndent(lines, index);
        if (stepIndent < 0)
            return result;

        var stepPropertyIndent = stepIndent + 2;
        while (index < lines.Length)
        {
            if (lines[index].IsIgnorable)
            {
                index++;
                continue;
            }

            if (lines[index].Indent < stepIndent)
                break;

            if (lines[index].Indent != stepIndent || !lines[index].Content.StartsWith("-", StringComparison.Ordinal))
                break;

            var step = new WorkflowStepContract();
            var firstProperty = lines[index].Content[1..].Trim();
            if (!string.IsNullOrWhiteSpace(firstProperty))
                ApplyStepProperty(step, lines[index], firstProperty, lines, ref index);
            else
                index++;

            while (index < lines.Length)
            {
                if (lines[index].IsIgnorable)
                {
                    index++;
                    continue;
                }

                if (lines[index].Indent <= stepIndent)
                    break;

                if (lines[index].Indent == stepPropertyIndent)
                {
                    var property = ReadKeyValue(lines[index]);
                    ApplyStepProperty(step, lines[index], property.Key + ":" + property.Value, lines, ref index);
                    continue;
                }

                index++;
            }

            result.Add(step);
        }

        return result;
    }

    private static int FindFirstContentIndent(SourceLine[] lines, int index)
    {
        while (index < lines.Length)
        {
            if (!lines[index].IsIgnorable)
                return lines[index].Indent;

            index++;
        }

        return -1;
    }

    private static void ApplyStepProperty(
        WorkflowStepContract step,
        SourceLine line,
        string propertyText,
        SourceLine[] lines,
        ref int index)
    {
        var separator = propertyText.IndexOf(':');
        if (separator < 0)
        {
            index++;
            return;
        }

        var key = propertyText[..separator].Trim();
        var value = propertyText[(separator + 1)..].Trim();
        switch (key)
        {
            case "name":
                step.Name = Unquote(value);
                index++;
                break;
            case "uses":
                step.Uses = Unquote(value);
                index++;
                break;
            case "if":
                step.Condition = Unquote(value);
                index++;
                break;
            case "run":
                step.Run = ReadBlockValue(lines, ref index, line.Indent, value);
                break;
            case "working-directory":
                step.WorkingDirectory = Unquote(value);
                index++;
                break;
            case "with":
                index++;
                ParseStepWith(step, lines, ref index, line.Indent);
                break;
            default:
                index++;
                break;
        }
    }

    private static void ParseStepWith(
        WorkflowStepContract step,
        SourceLine[] lines,
        ref int index,
        int parentIndent)
    {
        while (index < lines.Length)
        {
            if (lines[index].IsIgnorable)
            {
                index++;
                continue;
            }

            if (lines[index].Indent <= parentIndent)
                break;

            if (lines[index].Indent != parentIndent + 2)
            {
                index++;
                continue;
            }

            var property = ReadKeyValue(lines[index]);
            var value = property.Value;
            if (value is "|" or ">")
                value = ReadBlockValue(lines, ref index, 10, value);
            else
                index++;

            step.With[property.Key] = Unquote(value);
        }
    }

    private static string ReadBlockValue(SourceLine[] lines, ref int index, int parentIndent, string value)
    {
        if (value is not ("|" or ">"))
        {
            index++;
            return Unquote(value);
        }

        var content = new List<SourceLine>();
        var next = index + 1;
        while (next < lines.Length && (lines[next].IsIgnorable || lines[next].Indent > parentIndent))
        {
            content.Add(lines[next]);
            next++;
        }

        index = next;
        return string.Join(
            value == ">" ? " " : Environment.NewLine,
            content.Select(line => line.IsIgnorable ? string.Empty : line.Content.TrimEnd())).Trim();
    }

    private static int FindTopLevelKey(SourceLine[] lines, string key)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].IsIgnorable || lines[i].Indent != 0)
                continue;

            var property = ReadKeyValue(lines[i]);
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static (string Key, string Value) ReadKeyValue(SourceLine line)
    {
        var separator = line.Content.IndexOf(':');
        if (separator < 0)
            throw new InvalidDataException($"Line {line.Number} is not a YAML key/value pair.");

        return (Unquote(line.Content[..separator].Trim()), line.Content[(separator + 1)..].Trim());
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
            return value[1..^1];

        return value;
    }

    private readonly record struct SourceLine(int Number, int Indent, string Content, bool IsIgnorable, string Raw)
    {
        public static SourceLine Create(string raw, int number)
        {
            var withoutBom = number == 1 ? raw.TrimStart('\uFEFF') : raw;
            var indent = 0;
            while (indent < withoutBom.Length && withoutBom[indent] == ' ')
                indent++;

            if (indent < withoutBom.Length && withoutBom[indent] == '\t')
                throw new InvalidDataException($"Line {number} uses a tab for YAML indentation.");

            var content = withoutBom[indent..].TrimEnd();
            return new SourceLine(number, indent, content, string.IsNullOrWhiteSpace(content) || content.StartsWith('#'), withoutBom);
        }
    }
}

internal sealed class WorkflowJobContract(string id)
{
    public string Id { get; } = id;
    public string? RunsOn { get; set; }
    public Dictionary<string, string> Environment { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<WorkflowStepContract> Steps { get; } = [];

    public WorkflowStepContract Step(string name) =>
        Steps.FirstOrDefault(step => string.Equals(step.Name, name, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Workflow job '{Id}' has no step named '{name}'.");
}

internal sealed class WorkflowStepContract
{
    public string? Name { get; set; }
    public string? Uses { get; set; }
    public string? Run { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? Condition { get; set; }
    public Dictionary<string, string> With { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? OptionValue(string option)
    {
        if (string.IsNullOrWhiteSpace(Run) || string.IsNullOrWhiteSpace(option))
            return null;

        var marker = option.Trim() + " ";
        var start = Run.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        var value = Run[(start + marker.Length)..].TrimStart();
        if (value.Length == 0)
            return string.Empty;

        if (value[0] == '"')
        {
            var endQuote = value.IndexOf('"', 1);
            return endQuote < 0 ? value[1..] : value[1..endQuote];
        }

        var end = value.IndexOfAny([' ', '\r', '\n', '`']);
        return end < 0 ? value : value[..end];
    }

    public string WithValue(string key) =>
        With.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Workflow step '{Name ?? "<unnamed>"}' has no with value '{key}'.");
}

internal sealed class WorkflowTriggerContract(string name, IReadOnlyList<string> paths)
{
    public string Name { get; } = name;
    public IReadOnlyList<string> Paths { get; } = paths;
    public List<string> Values { get; } = [];
}
