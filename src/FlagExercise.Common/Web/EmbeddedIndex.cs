using System.Reflection;

namespace FlagExercise.Common.Web;

public static class EmbeddedIndex
{
    private static readonly string _template = LoadTemplate();

    public static string Html(string role)
    {
        var title = role.Equals("Tx", StringComparison.OrdinalIgnoreCase)
            ? "T(x) - Source / Mover Service"
            : "R(x) - Destination / Deleter Service";
        return _template
            .Replace("__ROLE__", role)
            .Replace("__TITLE__", title);
    }

    private static string LoadTemplate()
    {
        const string name = "FlagExercise.Common.Web.index.html";
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

