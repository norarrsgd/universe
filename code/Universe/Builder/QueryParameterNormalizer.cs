using System.Text.RegularExpressions;

namespace Universe.Builder;

internal static partial class QueryParameterNormalizer
{
    [GeneratedRegex(@"\G@\w+")]
    private static partial Regex ParameterPattern();

    internal static (string Text, IReadOnlyList<string> ParameterNames) Normalize(string sql)
    {
        StringBuilder normalized = new(sql.Length);
        List<string> names = [];
        char quote = '\0';

        for (int index = 0; index < sql.Length; index++)
        {
            char current = sql[index];
            if (quote != '\0')
            {
                normalized.Append(current);
                if (current == '\\' && index + 1 < sql.Length)
                    normalized.Append(sql[++index]);
                else if (current == quote)
                {
                    if (index + 1 < sql.Length && sql[index + 1] == quote)
                        normalized.Append(sql[++index]);
                    else
                        quote = '\0';
                }
                continue;
            }

            if (current is '\'' or '"')
                quote = current;
            else if (current == '@')
            {
                Match parameter = ParameterPattern().Match(sql, index);
                if (parameter.Success)
                {
                    names.Add(parameter.Value);
                    normalized.Append("@p");
                    index += parameter.Length - 1;
                    continue;
                }
            }

            normalized.Append(current);
        }

        return (normalized.ToString(), names);
    }
}
