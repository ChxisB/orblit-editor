namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Dart: orblit-editor/test/data_object_test.dart. The two bindings checked against the text Dart
// writes are added, since the C++ one has no Dart test.
public sealed class DataObjectTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_data");

    public void Dispose() => _root.Delete(recursive: true);

    private string At(params string[] parts) => Path.Combine([_root.FullName, .. parts]);

    private static DataObject Full() => new(
        "Ball settings",
        [
            new DataField("speed", DataType.Number, 12.5, "Metres a second"),
            new DataField("label", DataType.Text, "Ball"),
            new DataField("bouncy", DataType.Toggle, true),
            new DataField("tint", DataType.Colour, "#D9634F"),
            new DataField("spawn", DataType.Vector, new List<object?> { 0L, 1L, 2L }),
            new DataField("mesh", DataType.Asset, "meshes/ball.glb"),
        ],
        "How the ball behaves");

    [Fact]
    public void TheFileSurvivesARoundTripWithEveryKindOfValue()
    {
        var back = DataObject.Read(Full().ToText())!;

        Assert.Equal("Ball settings", back.Name);
        Assert.Equal("How the ball behaves", back.Note);
        Assert.Equal(12.5, back["speed"]!.AsNumber);
        Assert.Equal("Metres a second", back["speed"]!.Note);
        Assert.Equal("Ball", back["label"]!.AsText);
        Assert.True(back["bouncy"]!.AsToggle);
        Assert.Equal(new Colour(0xFFD9634F), back["tint"]!.AsColour);
        Assert.Equal(2, back["spawn"]!.AsVector.Z);
        Assert.Equal("meshes/ball.glb", back["mesh"]!.AsText);
    }

    [Fact]
    public void ItIsNotReadFromSomethingThatIsNotOne()
    {
        Assert.Null(DataObject.Read("nonsense"));
        Assert.Null(DataObject.Read("""{"kind":"orblit.prefab"}"""));
    }

    [Fact]
    public void AValueOfTheWrongShapeFallsBackRatherThanFailing()
    {
        var back = DataObject.Read(Full().ToText().Replace("12.5", "\"fast\"", StringComparison.Ordinal))!;

        // The field is still there and still a number. Only its value reset.
        Assert.Equal(DataType.Number, back["speed"]!.Type);
        Assert.Equal(0, back["speed"]!.AsNumber);
        // Reset in the field, not only read as 0, so writing it back does not keep the bad value.
        Assert.Equal(0.0, back["speed"]!.Value);
        Assert.Equal(6, back.Fields.Count);
    }

    [Fact]
    public void AFieldWithNoTypeIsLeftOutNotGuessedAt()
    {
        var back = DataObject.Read("""
            {"kind":"orblit.data","fields":[
              {"key":"a","type":"number","value":1},
              {"key":"b","value":2}
            ]}
            """)!;

        Assert.Equal(["a"], back.Fields.Select(field => field.Key));
    }

    [Fact]
    public void ANewOneIsNotEmpty() => Assert.NotEmpty(DataObject.Blank("settings").Fields);

    [Fact]
    public void ASecondFieldOfANameGetsANameOfItsOwn() =>
        Assert.NotEqual("value", DataObject.Blank("settings").Available("value"));

    [Fact]
    public void TheTypeScriptNamesATypeAfterTheObjectInPascalCase() =>
        Assert.Contains("export interface BallSettings {", Full().ToTypeScript("ball"), StringComparison.Ordinal);

    [Fact]
    public void TheTypeScriptGivesEveryFieldTheTypeItActuallyHolds()
    {
        var types = Full().ToTypeScript("ball");

        Assert.Contains("speed: number;", types, StringComparison.Ordinal);
        Assert.Contains("label: string;", types, StringComparison.Ordinal);
        Assert.Contains("bouncy: boolean;", types, StringComparison.Ordinal);
        Assert.Contains("spawn: [number, number, number];", types, StringComparison.Ordinal);
        // A colour and an asset are both paths through a string.
        Assert.Contains("tint: string;", types, StringComparison.Ordinal);
        Assert.Contains("mesh: string;", types, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTypeScriptCarriesTheNotesAcross() =>
        Assert.Contains("/** Metres a second */", Full().ToTypeScript("ball"), StringComparison.Ordinal);

    [Fact]
    public void ANameThatStartsWithADigitStillMakesALegalType() =>
        Assert.Contains("interface Data3rdFaction", new DataObject("3rd faction", []).ToTypeScript("f"), StringComparison.Ordinal);

    [Fact]
    public void TheTypeScriptSaysItWasGeneratedSoNobodyEditsIt() =>
        Assert.Contains("Do not edit", Full().ToTypeScript("ball"), StringComparison.Ordinal);

    private static DataObject Probe() => new(
        "Ball settings",
        [
            new DataField("speed", DataType.Number, 12.5, "Metres a second"),
            new DataField("bouncy", DataType.Toggle, true),
            new DataField("tint", DataType.Colour, "#D9634F"),
            new DataField("spawn", DataType.Vector, new List<object?> { 0L, 1L, 2L }),
        ],
        "How it behaves");

    // The text Dart's toTypeScript wrote for Probe on 2026-10-28.
    [Fact]
    public void TheTypeScriptIsTheTextTheDartEditorWrites() => Assert.Equal(
        "// Written by the editor from ball.odata. Do not edit.\n"
        + "//\n"
        + "// Ball settings \u2014 How it behaves\n"
        + "\n"
        + "export interface BallSettings {\n"
        + "  /** Metres a second */\n"
        + "  speed: number;\n"
        + "  bouncy: boolean;\n"
        + "  tint: string;\n"
        + "  spawn: [number, number, number];\n"
        + "}\n"
        + "\n"
        + "declare const data: BallSettings;\n"
        + "export default data;\n",
        Probe().ToTypeScript("ball"));

    // The text Dart's toCpp wrote for Probe on 2026-10-28. It has no Dart test of its own.
    [Fact]
    public void TheCppIsTheTextTheDartEditorWrites() => Assert.Equal(
        "// Written by the editor from data/ball.odata. Do not edit.\n"
        + "//\n"
        + "// Ball settings \u2014 How it behaves\n"
        + "\n"
        + "#pragma once\n"
        + "\n"
        + "#include \"orblit_script.h\"\n"
        + "\n"
        + "/// Each value is resolved to its address once and read through the\n"
        + "/// pointer after that: the read costs a load from memory, and the\n"
        + "/// value behind it still changes when somebody edits the file. That\n"
        + "/// is what makes these safe to call inside a loop over everything.\n"
        + "namespace BallSettings {\n"
        + "\n"
        + "inline constexpr const char *asset = \"data/ball.odata\";\n"
        + "\n"
        + "/// Metres a second\n"
        + "inline double speed(double fallback = 0) {\n"
        + "  static const double *at =\n"
        + "      ::orblit::number_at(asset, \"speed\", 0.0);\n"
        + "  return at ? *at : fallback;\n"
        + "}\n"
        + "\n"
        + "inline bool bouncy(bool fallback = false) {\n"
        + "  static const bool *at =\n"
        + "      ::orblit::toggle_at(asset, \"bouncy\", false);\n"
        + "  return at ? *at : fallback;\n"
        + "}\n"
        + "\n"
        + "inline const char *tint() {\n"
        + "  static const char *const *at =\n"
        + "      ::orblit::text_at(asset, \"tint\");\n"
        + "  return at ? *at : nullptr;\n"
        + "}\n"
        + "\n"
        + "inline double spawn(int axis, double fallback = 0) {\n"
        + "  static const double *at[3] = {\n"
        + "      ::orblit::number_at(asset, \"spawn.x\", 0.0),\n"
        + "      ::orblit::number_at(asset, \"spawn.y\", 0.0),\n"
        + "      ::orblit::number_at(asset, \"spawn.z\", 0.0)};\n"
        + "  if (axis < 0 || axis > 2 || !at[axis]) return fallback;\n"
        + "  return *at[axis];\n"
        + "}\n"
        + "\n"
        + "}  // namespace BallSettings\n",
        Probe().ToCpp("data/ball"));

    [Fact]
    public void TheStoreReadsOneFromTheProject()
    {
        File.WriteAllText(At("ball.odata"), Full().ToText());

        Assert.Equal("Ball settings", new DataStore(_root.FullName)["ball.odata"]!.Name);
    }

    [Fact]
    public void AMissingFileIsNullRatherThanACrash() => Assert.Null(new DataStore(_root.FullName)["nowhere.odata"]);

    [Fact]
    public void TheStoreReadsOnceAndHoldsOnToIt()
    {
        File.WriteAllText(At("ball.odata"), Full().ToText());
        var store = new DataStore(_root.FullName);
        Assert.Equal("Ball settings", store["ball.odata"]!.Name);

        File.WriteAllText(At("ball.odata"), new DataObject("Changed", []).ToText());

        // Still the first read: forty objects asking on every frame must not be forty file reads.
        Assert.Equal("Ball settings", store["ball.odata"]!.Name);
        store.Forget("ball.odata");
        Assert.Equal("Changed", store["ball.odata"]!.Name);
    }

    [Fact]
    public void WritingTellsWhateverIsShowingTheValue()
    {
        var store = new DataStore(_root.FullName);
        var told = 0;
        store.Changed += () => told++;

        Assert.Null(store.Write("ball.odata", Full()));

        Assert.Equal(1, told);
        Assert.True(File.Exists(At("ball.odata")));
        // And the written one is what comes back, without going to disk again.
        Assert.Equal("Ball settings", store["ball.odata"]!.Name);
    }

    [Fact]
    public void WritingIntoAFolderThatIsNotThereMakesIt()
    {
        Assert.Null(new DataStore(_root.FullName).Write("data/deep/ball.odata", Full()));

        Assert.True(File.Exists(At("data", "deep", "ball.odata")));
    }
}
