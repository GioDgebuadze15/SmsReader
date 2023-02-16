namespace SmsSender.ParserService;

public class IntegerParser : CustomParser<int?>
{
    public override int? Parse(string toParse, string? format)
    {
        if (format != null) return null;
        if (int.TryParse(toParse, out var parsedNumber))
        {
            return parsedNumber;
        }

        return null;
    }
}