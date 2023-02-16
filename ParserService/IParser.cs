namespace SmsSender.ParserService;

public abstract class CustomParser<T>
{
    public abstract T? Parse(string toParse, string? format);
}