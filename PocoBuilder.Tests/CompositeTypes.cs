namespace PocoBuilder.Tests;

// Note: CompositeTypes is an experiment for exceptionless programming.
// Throwing exceptions is an expensive operation. This is an idea for
// having exception classes as a optional return type.

// It's not limited to exceptions though - but I recommend
// against using this for anything else as I don't think
// it would produce very readable code.
// (Emphasis on think! I am often wrong!)

[TestClass]
public class CompositeTypes
{
    CompositeType<string, Exception> result;

    [TestMethod]
    public void Test1_SuccessType()
    {
        result = "All went well";
        result.Resolve(
            success => Assert.IsInstanceOfType(success, typeof(string)),
            error => Assert.Fail()
        );
    }

    [TestMethod]
    public void Test2_ErrorType()
    {
        result = new NullReferenceException("Oh no");
        result.Resolve(
            success => Assert.Fail(),
            error => Assert.IsInstanceOfType(error, typeof(NullReferenceException))
        );
    }

    [TestMethod]
    public void Test3_ThrowWhenUnresolved()
    {
        result = default;
        Assert.ThrowsException<UnresolvableCompositeTypeException>(() => result.Resolve(
            success => Assert.Fail(),
            error => Assert.Fail()
        ));
    }

    [TestMethod]
    public void Test4_TripleCompositeType()
    {
        static void checkBoolean(bool v) => Assert.IsInstanceOfType(v, typeof(bool));
        static void checkInt(int v) => Assert.IsInstanceOfType(v, typeof(int));
        static void checkString(string? v) => Assert.IsInstanceOfType(v, typeof(string));
        static void failBoolean(bool v) => Assert.Fail();
        static void failInt(int v) => Assert.Fail();
        static void failString(string? v) => Assert.Fail();

        CompositeType<bool, int, string> composite = default;
        Assert.ThrowsException<UnresolvableCompositeTypeException>(()
            => composite.Resolve(failBoolean, failInt, failString));

        composite = true;
        composite.Resolve(checkBoolean, failInt, failString);

        composite = 4;
        composite.Resolve(failBoolean, checkInt, failString);

        composite = "This is a string";
        composite.Resolve(failBoolean, failInt, checkString);
    }
}
