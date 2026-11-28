using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Demo.Fp0801_SerializerAndRegex;

[Fprog.FunctionalCore]
public sealed record Order(string Id, ImmutableArray<decimal> Lines);

// System.Text.Json and System.Text.RegularExpressions count as Pure because they lie below System.Text.
[Fprog.FunctionalCore]
public static class Boundary
{
    // B14: JsonException for malformed text; the serializer works by reflection.
    public static Order Read(string json) => JsonSerializer.Deserialize<Order>(json) ?? new Order("", []);

    // B15: RegexParseException when the pattern is invalid.
    public static bool Matches(string pattern, string input) => Regex.IsMatch(input, pattern);

    // C6: follows the current culture. For input "I" and letter "i" the result is True under en-US and False under tr-TR.
    public static bool HasLetter(string input, string letter) => Regex.IsMatch(input, letter, RegexOptions.IgnoreCase);
}
