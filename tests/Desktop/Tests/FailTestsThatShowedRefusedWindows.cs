using System.Reflection;
using Memoria.ToDo.Desktop;
using Xunit.v3;

[assembly: FailTestsThatShowedRefusedWindows]

namespace Memoria.ToDo.Desktop;

/// <summary>Fails each test, once it ends, that showed a window the <see cref="ScreenCollectionGuard"/> refused, with the guard's message.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class FailTestsThatShowedRefusedWindows : BeforeAfterTestAttribute
{
    public override void After(MethodInfo methodUnderTest, IXunitTest test) =>
        ThrowIfRefused(test.UniqueID, WindowRefusals.Shared);

    /// <summary>Throws <see cref="InvalidOperationException"/> with the messages <paramref name="refusals"/> holds for the test, when it holds any.</summary>
    public static void ThrowIfRefused(string testId, WindowRefusals refusals)
    {
        var messages = refusals.Take(testId);
        if (messages.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, messages));
        }
    }
}
