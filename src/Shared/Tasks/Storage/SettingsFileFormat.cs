using System.Text.Json;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The JSON of <c>settings.json</c>: a version and three settings, each a value and the time it changed.</summary>
internal static class SettingsFileFormat
{
    public static byte[] Write(SharedSettings settings) => JsonFields.Write(writer =>
    {
        writer.WriteNumber("version", JsonFields.CurrentVersion);
        WriteSetting(writer, "deletionDelayDays", settings.DeletionDelayDays, (w, days) => w.WriteNumber("value", days));
        WriteSetting(writer, "addNewTaskShortcut", settings.AddNewTaskShortcut, WriteText);
        WriteSetting(writer, "manageTasksShortcut", settings.ManageTasksShortcut, WriteText);
    });

    /// <summary>Throws <see cref="InvalidDataException"/> when the file isn't settings this app can read.</summary>
    public static SharedSettings Read(byte[] bytes)
    {
        using var document = JsonFields.Parse(bytes);
        var root = document.RootElement;
        JsonFields.RequireSupportedVersion(root);
        var days = ReadSetting(root, "deletionDelayDays", JsonFields.Int32);
        if (days.Value is < SharedSettings.MinDeletionDelayDays or > SharedSettings.MaxDeletionDelayDays)
            throw new InvalidDataException("'deletionDelayDays' is out of range.");
        return new SharedSettings(days,
            ReadSetting(root, "addNewTaskShortcut", JsonFields.OptionalString),
            ReadSetting(root, "manageTasksShortcut", JsonFields.OptionalString));
    }

    private static void WriteSetting<T>(Utf8JsonWriter writer, string name, Setting<T> setting, Action<Utf8JsonWriter, T> value)
    {
        writer.WriteStartObject(name);
        value(writer, setting.Value);
        JsonFields.WriteTime(writer, "updated", setting.Updated);
        writer.WriteEndObject();
    }

    private static void WriteText(Utf8JsonWriter writer, string? text)
    {
        if (text is null)
            writer.WriteNull("value");
        else
            writer.WriteString("value", text);
    }

    private static Setting<T> ReadSetting<T>(JsonElement root, string name, Func<JsonElement, string, T> value)
    {
        var setting = JsonFields.Object(root, name);
        return new Setting<T>(value(setting, "value"), JsonFields.Time(setting, "updated"));
    }
}
