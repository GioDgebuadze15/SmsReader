using System.Globalization;

namespace SmsSender.ParserService;

public class DateTimeParser : CustomParser<DateTimeOffset?>
{
    public override DateTimeOffset? Parse(string toParse, string? format)
    {
        if (format == null) return null;
        if (DateTimeOffset.TryParseExact(toParse, format, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        return null;
    }
}