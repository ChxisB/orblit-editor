namespace Orblit.Editor.Panels;

using System.Globalization;
using Orblit.Motion;

/// <summary>What kind of test a condition is, which is what a person chooses.</summary>
public enum ConditionKind
{
    Always,
    Above,
    Below,
    On,
    Through,
    All,
    Any,
    Not,
}

/// <summary>The words and the groupings of <see cref="ConditionKind"/>.</summary>
public static class ConditionKinds
{
    public static string Label(this ConditionKind kind) => kind switch
    {
        ConditionKind.Always => "Always",
        ConditionKind.Above => "Above",
        ConditionKind.Below => "Below",
        ConditionKind.On => "Is on",
        ConditionKind.Through => "Laps through",
        ConditionKind.All => "All of",
        ConditionKind.Any => "Any of",
        _ => "Not",
    };

    /// <summary>Whether it is made of other conditions.</summary>
    public static bool IsComposite(this ConditionKind kind) => kind is ConditionKind.All or ConditionKind.Any or ConditionKind.Not;

    /// <summary>Whether it tests one of the blend's inputs.</summary>
    public static bool ReadsInput(this ConditionKind kind) => kind is ConditionKind.Above or ConditionKind.Below or ConditionKind.On;
}

/// <summary>
/// A condition as the editor reads and rebuilds it. A condition has no public kind, so the kind is read
/// from the keys its JSON has, the way a file tells them apart.
/// </summary>
public static class BlendConditionModel
{
    private static readonly (string Key, ConditionKind Kind)[] Keyed =
    [
        ("all", ConditionKind.All),
        ("any", ConditionKind.Any),
        ("not", ConditionKind.Not),
        ("through", ConditionKind.Through),
        ("above", ConditionKind.Above),
        ("below", ConditionKind.Below),
    ];

    public static ConditionKind KindOf(BlendCondition condition)
    {
        var json = condition.ToJson();
        foreach (var (key, kind) in Keyed)
        {
            if (json.ContainsKey(key))
            {
                return kind;
            }
        }

        return json.Count == 0 ? ConditionKind.Always : ConditionKind.On;
    }

    /// <summary>The input a test reads, or an empty name for a condition with none.</summary>
    public static string InputOf(BlendCondition condition) =>
        condition.ToJson().TryGetValue("input", out var input) && input is string name ? name : string.Empty;

    /// <summary>The number a test compares with or waits for, or zero.</summary>
    public static double NumberOf(BlendCondition condition)
    {
        var json = condition.ToJson();
        var value = json.GetValueOrDefault("above") ?? json.GetValueOrDefault("below") ?? json.GetValueOrDefault("through");
        return value is double number ? number : 0;
    }

    /// <summary>What a composite is made of, outermost first.</summary>
    public static IReadOnlyList<BlendCondition> PartsOf(BlendCondition condition)
    {
        var json = condition.ToJson();
        var raw = json.GetValueOrDefault("all") ?? json.GetValueOrDefault("any") ?? json.GetValueOrDefault("not");
        IEnumerable<object?> parts = raw is IReadOnlyList<object?> list ? list : [raw];
        return
        [
            .. parts.OfType<IReadOnlyDictionary<string, object?>>()
                .Select(static part => BlendCondition.FromJson(part, "a condition", []) ?? BlendCondition.Always),
        ];
    }

    /// <summary>A condition of <paramref name="kind"/>, keeping what <paramref name="old"/> has that still means something.</summary>
    public static BlendCondition OfKind(ConditionKind kind, BlendCondition old) =>
        kind.IsComposite() ? Composite(kind, old) : Simple(kind, old);

    /// <summary>
    /// <paramref name="condition"/> testing <paramref name="input"/> instead, or the same condition when it
    /// has no input or <paramref name="input"/> is no name.
    /// </summary>
    public static BlendCondition Reading(BlendCondition condition, string input)
    {
        if (input.Length == 0)
        {
            return condition;
        }

        var value = NumberOf(condition);
        return KindOf(condition) switch
        {
            ConditionKind.Above => BlendCondition.Above(input, value),
            ConditionKind.Below => BlendCondition.Below(input, value),
            ConditionKind.On => BlendCondition.On(input),
            _ => condition,
        };
    }

    /// <summary><paramref name="condition"/> with its number set to <paramref name="value"/>.</summary>
    public static BlendCondition Numbered(BlendCondition condition, double value)
    {
        if (!double.IsFinite(value))
        {
            return condition;
        }

        var input = InputOf(condition);
        return KindOf(condition) switch
        {
            ConditionKind.Above => BlendCondition.Above(input, value),
            ConditionKind.Below => BlendCondition.Below(input, value),
            ConditionKind.Through => BlendCondition.Through(value),
            _ => condition,
        };
    }

    /// <summary>A composite with <paramref name="parts"/> in place of what it was made of.</summary>
    public static BlendCondition MadeOf(BlendCondition condition, IReadOnlyList<BlendCondition> parts) => KindOf(condition) switch
    {
        ConditionKind.All => BlendCondition.AllOf(parts),
        ConditionKind.Any => BlendCondition.AnyOf(parts),
        ConditionKind.Not => BlendCondition.Not(parts.Count > 0 ? parts[0] : BlendCondition.Always),
        _ => condition,
    };

    /// <summary>A condition as a short phrase, such as <c>speed above 0.1</c>.</summary>
    public static string Describe(BlendCondition condition)
    {
        var kind = KindOf(condition);
        return kind.IsComposite() ? DescribeComposite(kind, condition) : DescribeSimple(kind, condition);
    }

    /// <summary>A number with no trailing zeros, to three places.</summary>
    public static string NumberText(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static BlendCondition Simple(ConditionKind kind, BlendCondition old)
    {
        var named = InputOf(old) is { Length: > 0 } input ? input : "input";
        return kind switch
        {
            ConditionKind.Above => BlendCondition.Above(named, NumberOf(old)),
            ConditionKind.Below => BlendCondition.Below(named, NumberOf(old)),
            ConditionKind.On => BlendCondition.On(named),
            ConditionKind.Through => BlendCondition.Through(KindOf(old) == ConditionKind.Through ? NumberOf(old) : 1),
            _ => BlendCondition.Always,
        };
    }

    // An always has nothing to nest, so a group made out of one starts empty.
    private static BlendCondition Composite(ConditionKind kind, BlendCondition old)
    {
        IReadOnlyList<BlendCondition> held = KindOf(old) == ConditionKind.Always ? [] : [old];
        return kind switch
        {
            ConditionKind.All => BlendCondition.AllOf(held),
            ConditionKind.Any => BlendCondition.AnyOf(held),
            _ => BlendCondition.Not(old),
        };
    }

    private static string DescribeSimple(ConditionKind kind, BlendCondition condition)
    {
        var input = InputOf(condition);
        var number = NumberText(NumberOf(condition));
        return kind switch
        {
            ConditionKind.Above => $"{input} above {number}",
            ConditionKind.Below => $"{input} below {number}",
            ConditionKind.On => $"{input} is on",
            ConditionKind.Through => NumberOf(condition) == 1 ? "when it is done" : $"after {number} laps",
            _ => "always",
        };
    }

    private static string DescribeComposite(ConditionKind kind, BlendCondition condition)
    {
        var parts = PartsOf(condition).Select(Describe).ToList();
        return kind switch
        {
            ConditionKind.Not => $"not {string.Concat(parts)}",
            _ when parts.Count == 0 => "always",
            ConditionKind.All => string.Join(" and ", parts),
            _ => string.Join(" or ", parts),
        };
    }
}
