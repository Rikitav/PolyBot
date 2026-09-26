namespace PolyBot.Tests;

[TestClass]
public sealed class CallbackPatternMatcherTests
{
    private enum MenuAction
    {
        None = 0,
        Delete,
        Archive,
    }

    [TestMethod]
    public void Match_SingleIntCaptureWithCustomSeparator()
    {
        bool matched = CallbackPatternMatcher.Match("item;42", ';', "item;{id}", out int id);

        Assert.IsTrue(matched);
        Assert.AreEqual(42, id);
    }

    [TestMethod]
    public void Match_SingleCaptureDefaultSeparator()
    {
        bool matched = CallbackPatternMatcher.Match("item:42", "item:{id}", out int id);

        Assert.IsTrue(matched);
        Assert.AreEqual(42, id);
    }

    [TestMethod]
    public void Match_MultipleCapturesBindPositionally()
    {
        bool matched = CallbackPatternMatcher.Match("item;42;delete", ';', "item;{id};{action}", out int id, out string action);

        Assert.IsTrue(matched);
        Assert.AreEqual(42, id);
        Assert.AreEqual("delete", action);
    }

    [TestMethod]
    public void Match_LiteralMismatchReturnsFalse()
    {
        bool matched = CallbackPatternMatcher.Match("cart;42", ';', "item;{id}", out int id);

        Assert.IsFalse(matched);
        Assert.AreEqual(0, id);
    }

    [TestMethod]
    public void Match_SegmentCountMismatchReturnsFalse()
    {
        Assert.IsFalse(CallbackPatternMatcher.Match("item;42;extra", ';', "item;{id}", out int _));
        Assert.IsFalse(CallbackPatternMatcher.Match("item", ';', "item;{id}", out int _));
    }

    [TestMethod]
    public void Match_UnparsableCaptureReturnsFalseWithDefault()
    {
        bool matched = CallbackPatternMatcher.Match("item;abc", ';', "item;{id}", out int id);

        Assert.IsFalse(matched);
        Assert.AreEqual(0, id);
    }

    [TestMethod]
    public void Match_EnumCaptureParsesCaseInsensitively()
    {
        bool matched = CallbackPatternMatcher.Match("item;1;archive", ';', "item;{id};{action}", out int id, out MenuAction action);

        Assert.IsTrue(matched);
        Assert.AreEqual(1, id);
        Assert.AreEqual(MenuAction.Archive, action);
    }

    [TestMethod]
    public void Match_EnumCaptureRejectsUnknownName()
    {
        Assert.IsFalse(CallbackPatternMatcher.Match("item;1;bogus", ';', "item;{id};{action}", out int _, out MenuAction _));
    }

    [TestMethod]
    public void Match_DecimalCaptureUsesInvariantCulture()
    {
        bool matched = CallbackPatternMatcher.Match("pay;19.99", ';', "pay;{amount}", out decimal amount);

        Assert.IsTrue(matched);
        Assert.AreEqual(19.99m, amount);
    }

    [TestMethod]
    public void Match_StringCaptureBindsVerbatim()
    {
        bool matched = CallbackPatternMatcher.Match("menu;user settings", ';', "menu;{section}", out string section);

        Assert.IsTrue(matched);
        Assert.AreEqual("user settings", section);
    }

    [TestMethod]
    public void Match_InlineConstraintSurvivesColonSeparator()
    {
        // The ':' inside {id:int} must not be treated as a segment separator.
        bool matched = CallbackPatternMatcher.Match("item:42", "item:{id:int}", out int id);

        Assert.IsTrue(matched);
        Assert.AreEqual(42, id);
    }

    [TestMethod]
    public void Match_WildcardCapturesTheRest()
    {
        bool matched = CallbackPatternMatcher.Match("logs;a/b/c", ';', "logs;{*path}", out string path);

        Assert.IsTrue(matched);
        Assert.AreEqual("a/b/c", path);
    }

    [TestMethod]
    public void Match_WildcardAllowsEmptyTail()
    {
        bool matched = CallbackPatternMatcher.Match("logs;", ';', "logs;{*path}", out string path);

        Assert.IsTrue(matched);
        Assert.AreEqual(string.Empty, path);
    }

    [TestMethod]
    public void Match_WildcardAcceptsExtraSegments()
    {
        bool matched = CallbackPatternMatcher.Match("logs;a;b;c", ';', "logs;{*path}", out string path);

        Assert.IsTrue(matched);
        Assert.AreEqual("a;b;c", path);
    }

    [TestMethod]
    public void Match_WildcardCombinedWithTypedCaptures()
    {
        bool matched = CallbackPatternMatcher.Match("go;7;north;through the woods", ';', "go;{step};{*direction}", out int step, out string direction);

        Assert.IsTrue(matched);
        Assert.AreEqual(7, step);
        Assert.AreEqual("north;through the woods", direction);
    }

    [TestMethod]
    public void Match_WildcardWithNonStringTargetThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CallbackPatternMatcher.Match("logs;a/b", ';', "logs;{*path}", out int _));
    }

    [TestMethod]
    public void Match_ArityMismatchThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CallbackPatternMatcher.Match("item;42;delete", ';', "item;{id};{action}", out int _));
        Assert.ThrowsExactly<ArgumentException>(
            () => CallbackPatternMatcher.Match("item;42", ';', "item;{id}", out int _, out string _));
    }

    [TestMethod]
    public void Match_MalformedTemplatesThrow()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CallbackPatternMatcher.Match("a;b", ';', "a;{id", out int _));
        Assert.ThrowsExactly<ArgumentException>(() => CallbackPatternMatcher.Match("a;b", ';', "a;{}", out int _));
        Assert.ThrowsExactly<ArgumentException>(() => CallbackPatternMatcher.Match("a;b", ';', "a;{*};x", out string _, out int _));
        Assert.ThrowsExactly<ArgumentException>(() => CallbackPatternMatcher.Match("a;b;x", ';', "a;{*p};x", out string _));
        Assert.ThrowsExactly<ArgumentException>(() => CallbackPatternMatcher.Match("a;;b", ';', "a;;b", out int _));
        Assert.ThrowsExactly<ArgumentException>(() => CallbackPatternMatcher.Match("a;b", ';', "a;{id:}", out int _));
    }

    [TestMethod]
    public void Match_TypeWithoutTryParseThrows()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => CallbackPatternMatcher.Match("u;https://example.com", ';', "u;{uri}", out Uri _));
    }

    [TestMethod]
    public void Match_CustomTryParseTypeResolvesViaReflection()
    {
        bool matched = CallbackPatternMatcher.Match(
            "g;550e8400-e29b-41d4-a716-446655440000",
            ';',
            "g;{id}",
            out Guid id);

        Assert.IsTrue(matched);
        Assert.AreEqual(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), id);
    }

    [TestMethod]
    public void Match_BoolCaptureUsesTryParseFallback()
    {
        bool matched = CallbackPatternMatcher.Match("set;true", ';', "set;{value}", out bool value);

        Assert.IsTrue(matched);
        Assert.IsTrue(value);
    }

    [TestMethod]
    public void Match_DataWithTooManySegmentsReturnsFalse()
    {
        string data = string.Join(";", Enumerable.Repeat("x", 34));

        Assert.IsFalse(CallbackPatternMatcher.Match(data, ';', "{v}", out string _));
    }

    [TestMethod]
    public void Match_LiteralOnlyTemplate()
    {
        Assert.IsTrue(CallbackPatternMatcher.Match("cart;checkout", ';', "cart;checkout"));
        Assert.IsFalse(CallbackPatternMatcher.Match("cart;cancel", ';', "cart;checkout"));
    }
}
