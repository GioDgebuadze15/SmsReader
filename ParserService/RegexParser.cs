using System.Text.RegularExpressions;

namespace SmsSender.ParserService;

public static class RegexParser
{
    private static readonly Regex RegexForSms = new(
        @"(?:\+CMGL: )(?<index>[\d]{1,2})[,]+""[\w ]+"",""(?<sender>[+\w]*)"","""",""(?<time>[+\w\/ :]*)""(?<text>(?s).*?)(?:\r?\n){2}",
        RegexOptions.Singleline);

    private static readonly Regex RegexForFine = new(
        @": (?<carNumber>[\d\w\-_]+), [\w]+ [\w]+[\w-]+[^\d](?<article>[\d-]+)[\w .]+\:(?<street>[\w\d . :]+),[a-zA-Z :]+(?<time>[+\w\/ :]*), [a-zA-Z: ]+\: (?<receiptNumber>[\w]+), [a-zA-Z:]+ (?<amount>[\d]+).+chabarebidan (?<term>[\d]+)",
        RegexOptions.Singleline);

    private static readonly Regex RegexForReminder = new(
        @"qvitris (?<receiptNumber>[\w]+)[a-zA-Z ]+(?<lastDateOfPayment>[\d.]+)", RegexOptions.Compiled);


    public static MatchCollection ParseSms(string text)
        => RegexForSms.Matches(text);

    public static Match ParseSmsForFine(string text)
        => RegexForFine.Match(text);

    public static Match ParseSmsForReminder(string text)
        => RegexForReminder.Match(text);
}