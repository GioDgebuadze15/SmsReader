using System.IO.Ports;
using System.Text.RegularExpressions;
using SmsSender.EntityFramework;
using SmsSender.ParserService;

namespace SmsSender;

public class SmsRepository
{
    private string? PortName { get; set; }

    private const string DateTimeFormatYearMonthDayTime = "yyyy/MM/dd HH:mm:ss";
    private const string DateTimeFormatDayMonthYearTime = "dd/MM/yyyy HH:mm:ss";

    private readonly AppDbContext _ctx;
    private const int BaudRate = 38400;
    private readonly DateTimeParser _dateTimeParser = new();
    private readonly IntegerParser _integerParser = new();

    public SmsRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task Run()
    {
        if (!SetComPort())
        {
            Console.WriteLine("Port can't be found!");
            return;
        }

        if (PortName != null)
        {
            using var serialPort = new SerialPort(PortName, BaudRate);
            Init(serialPort);
            await ReadSms(serialPort);
        }
    }

    private static void Init(SerialPort serialPort)
    {
        serialPort.Open();
        Console.WriteLine("Port has successfully opened");

        if (!ExecuteCommand(serialPort, "AT", 200)) return;
        ExecuteCommand(serialPort, "ATE0", 100);

        var time = DateTime.Now.ToLocalTime().ToString("yy/MM/dd,HH:mm:sszz");
        ExecuteCommand(serialPort, $"AT+CCLK=\"{time}\"", 100);
        ExecuteCommand(serialPort, "AT+CCLK?", 200);

        Console.WriteLine("####################################");
        Console.WriteLine("Initialization finished successfully");
        Console.WriteLine("####################################");
    }

    private static bool ExecuteCommand(SerialPort serialPort, string command, int timeout)
    {
        var message = command + Environment.NewLine;
        Console.WriteLine($"Message: {message.Trim()}");
        serialPort.Write(message);
        Thread.Sleep(timeout);

        var response = serialPort.ReadExisting();
        if (!response.Contains("OK"))
        {
            Console.WriteLine("Response: " + response.Trim());
            return false;
        }

        Console.WriteLine("Response: " + response.Trim());
        return true;
    }

    private static string ExecuteCommandWithResponse(SerialPort serialPort, string command, int timeout)
    {
        var message = command + Environment.NewLine;
        Console.WriteLine($"Message: {message.Trim()}");
        serialPort.Write(message);
        Thread.Sleep(timeout);

        var response = serialPort.ReadExisting();
        return response.Trim();
    }

    private async Task ReadSms(SerialPort serialPort)
    {
        ExecuteCommand(serialPort, "AT+CMGF=1", 200);

        var response = ExecuteCommandWithResponse(serialPort, "AT+CMGL=\"ALL\"", 5000);

        var regex =
            new Regex(
                @"(?:\+CMGL: )(?<index>[\d]{1,2})[,]+""[\w ]+"",""(?<sender>[+\w]*)"","""",""(?<time>[+\w\/ :]*)""(?<text>(?s).*?)(?:\r?\n){2}",
                RegexOptions.Singleline);
        var matches = regex.Matches(response);
        if (matches.Count < 1)
            Console.WriteLine("No sms found");

        foreach (Match match in matches)
        {
            if (!match.Success) continue;

            var index = match.Groups["index"].Value.Trim();
            var sender = match.Groups["sender"].Value.Trim();
            var time = match.Groups["time"].Value.Trim();
            var text = match.Groups["text"].Value.Trim();

            var sms = ParseSms(sender, time, text);
            if (!string.IsNullOrEmpty(sms.Text))
            {
                sms = ParseSmsText(sms);
            }

            var id = await SaveSmsIntoDatabase(sms);
            Console.WriteLine("Message has successfully saved into database");
            await DeleteSmsInPhone(serialPort, index, id);
            Console.WriteLine("Message has successfully removed from phone");
        }

        Console.WriteLine("####################################");
        Console.WriteLine("Program has successfully finished");
        Console.WriteLine("Press any key to close...");
        Console.WriteLine("####################################");
        Console.ReadKey();
    }


    private ReceivedSms ParseSms(string sender, string time, string text)
        => new()
        {
            Sender = sender,
            ReceivedDate = _dateTimeParser.Parse(time.Split('+')[0], DateTimeFormatYearMonthDayTime),
            Text = text,
        };

    private ReceivedSms ParseSmsText(ReceivedSms sms)
    {
        var regex = new Regex(
            @": (?<carNumber>[\d\w\-_]+), [\w]+ [\w]+[\w-]+[^\d](?<article>[\d-]+)[\w .]+\:(?<street>[\w\d . :]+),[a-zA-Z :]+(?<time>[+\w\/ :]*), [a-zA-Z: ]+\: (?<receiptNumber>[\w]+), [a-zA-Z:]+ (?<amount>[\d]+).+chabarebidan (?<term>[\d]+)",
            RegexOptions.Singleline);

        var match = regex.Match(sms.Text!);
        if (!match.Success)
        {
            Console.WriteLine("Cannot parse sms text");
            return sms;
        }

        ParseMatchedTextToSms(ref sms, match);

        return sms;
    }

    private async Task DeleteSmsInPhone(SerialPort serialPort, string index, int id)
    {
        var numberIndex = _integerParser.Parse(index, null);
        if (numberIndex == null) return;
        if (!ExecuteCommand(serialPort, $"AT+CMGD={numberIndex}", 1000)) return;
        await UpdateDeletedStatus(id);
    }

    private static void DeleteAllSmsInPhone(SerialPort serialPort)
        =>
            ExecuteCommand(serialPort, $"AT+QMGDA=\"DEL ALL\"", 1000);


    private bool SetComPort()
    {
        var portName = UsbSerialConverterInfo.GetPortName();
        if (portName == null && portName == "")
            return false;
        PortName = portName;
        return true;
    }

    private void ParseMatchedTextToSms(ref ReceivedSms sms, Match match)
    {
        sms.CarNumber = match.Groups["carNumber"].Value.Trim();
        sms.Article = match.Groups["article"].Value.Trim();
        sms.Street = match.Groups["street"].Value.Trim();
        sms.DateOfFine = _dateTimeParser.Parse(match.Groups["time"].Value.Trim(), DateTimeFormatDayMonthYearTime);
        sms.ReceiptNumber = match.Groups["receiptNumber"].Value.Trim();
        sms.Amount = _integerParser.Parse(match.Groups["amount"].Value.Trim(), null);
        sms.Term = _integerParser.Parse(match.Groups["term"].Value.Trim(), null);
        sms.Parsed = true;

        if (sms.DateOfFine != null && sms.Term != null)
        {
            sms.LastDateOfPayment = sms.DateOfFine.Value.AddDays(sms.Term.Value);
        }
    }

    private async Task UpdateDeletedStatus(int id)
    {
        var sms = _ctx.ReceivedSms.FirstOrDefault(x => x.Id.Equals(id));
        if (sms != null) sms.Deleted = true;
        await _ctx.SaveChangesAsync();
    }

    private async Task<int> SaveSmsIntoDatabase(ReceivedSms sms)
    {
        _ctx.Add(sms);
        await _ctx.SaveChangesAsync();
        return sms.Id;
    }
}