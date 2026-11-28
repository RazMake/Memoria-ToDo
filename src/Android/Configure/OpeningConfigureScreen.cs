using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.Configure;

/// <summary>Configure, shown by asking the opener for its activity.</summary>
internal sealed class OpeningConfigureScreen(IScreenOpener screens) : IConfigureScreen
{
    public void Show() => screens.Open(ConfigureActivityName.Value);
}
