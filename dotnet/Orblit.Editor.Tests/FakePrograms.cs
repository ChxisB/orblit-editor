namespace Orblit.Editor.Tests;

// Each call is written as its program and arguments joined by spaces.
internal sealed class FakePrograms : IPrograms
{
    public HashSet<string> Answers { get; } = [];

    public HashSet<string> Refused { get; } = [];

    public List<string> Asked { get; } = [];

    public List<string> Started { get; } = [];

    public bool Start(string program, IReadOnlyList<string> arguments)
    {
        if (Refused.Contains(program))
        {
            return false;
        }

        Started.Add(string.Join(' ', [program, .. arguments]));
        return true;
    }

    public bool Succeeds(string program, IReadOnlyList<string> arguments)
    {
        var call = string.Join(' ', [program, .. arguments]);
        Asked.Add(call);
        return Answers.Contains(call);
    }
}
