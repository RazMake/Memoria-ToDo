using System.Text;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>A parsed mention template: literal text and <c>{placeholders}</c>, with <c>{{</c> and <c>}}</c> for literal braces.</summary>
internal sealed class MentionTemplate
{
    private readonly IReadOnlyList<TemplateSegment> segments;

    private MentionTemplate(IReadOnlyList<TemplateSegment> segments) => this.segments = segments;

    /// <summary>The template, or null with the problem when it's empty, names an unknown field, or has an unmatched brace.</summary>
    public static MentionTemplate? Parse(string template, out string? problem)
    {
        problem = null;
        if (template.Length == 0)
        {
            problem = "The template is empty.";
            return null;
        }

        var segments = new List<TemplateSegment>();
        var literal = new StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c is '{' or '}' && i + 1 < template.Length && template[i + 1] == c)
            {
                literal.Append(c);
                i++;
            }
            else if (c == '}')
            {
                problem = "A '}' has no '{'.";
                return null;
            }
            else if (c == '{')
            {
                var end = template.IndexOf('}', i);
                var name = end < 0 ? "" : template[(i + 1)..end];
                if (end < 0 || name.Contains('{'))
                {
                    problem = "A '{' has no '}'.";
                    return null;
                }

                if (Placeholder(name) is not { } segment)
                {
                    problem = $"'{name}' isn't a known field.";
                    return null;
                }

                if (literal.Length > 0)
                    segments.Add(new LiteralSegment(literal.ToString()));
                literal.Clear();
                segments.Add(segment);
                i = end;
            }
            else
            {
                literal.Append(c);
            }
        }

        if (literal.Length > 0)
            segments.Add(new LiteralSegment(literal.ToString()));
        return new MentionTemplate(segments);
    }

    /// <summary>The text for the contact, or its full name when it lacks any value the template names.</summary>
    public string Render(Contact contact, IReadOnlyList<DataType> dataTypes)
    {
        var text = new StringBuilder();
        foreach (var segment in segments)
        {
            if (segment.Text(contact, dataTypes) is not { } part)
                return contact.FullName;
            text.Append(part);
        }

        return text.ToString();
    }

    private static TemplateSegment? Placeholder(string name)
    {
        var dot = name.IndexOf('.');
        if (dot < 0)
            return ContactFields.Named(name) is { } field ? new FieldSegment(field) : null;

        var key = KeyFields.All.FirstOrDefault(candidate => candidate.Prefix == name[..dot]);
        return key is not null && dot + 1 < name.Length ? new EntrySegment(key, name[(dot + 1)..]) : null;
    }
}
