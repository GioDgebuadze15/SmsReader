using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using SmsSender.EntityFramework;

namespace SmsSender;

public class SmsRepository
{
    private string? PortName { get; set; }

    private const string Format = "yyyy/MM/dd HH:mm:ss";

    private readonly AppDbContext _ctx;
    private const int BaudRate = 38400;

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

        foreach (Match match in matches)
        {
            if (!match.Success) continue;

            var index = match.Groups["index"].Value.Trim();
            var sender = match.Groups["sender"].Value.Trim();
            var time = match.Groups["time"].Value.Trim();
            var text = match.Groups["text"].Value.Trim();

            Console.WriteLine("Message has successfully saved into database");
            var id = await SaveSmsIntoDatabase(sender, time, text);
            // await DeleteSmsInPhone(serialPort,index,id);
            Console.WriteLine("Message has successfully removed from phone");
        }
    }

    private async Task<int> SaveSmsIntoDatabase(string sender, string time, string text)
    {
        var sms = new ReceivedSms
        {
            Sender = sender,
            Text = text
        };

        if (DateTimeOffset.TryParseExact(time.Split('+')[0], Format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dateTimeOffset))
        {
            sms.ReceivedDate = dateTimeOffset;
        }

        _ctx.Add(sms);
        await _ctx.SaveChangesAsync();
        return sms.Id;
    }

    private async Task DeleteSmsInPhone(SerialPort serialPort, string index, int id)
    {
        if (int.TryParse(index, out var numberIndex))
        {
            if (!ExecuteCommand(serialPort, $"AT+CMGD={numberIndex}", 5000)) return;
            await UpdateDeletedStatus(id);
        }
    }

    private bool SetComPort()
    {
        var portName = UsbSerialConverterInfo.GetPortName();
        if (portName == null && portName == "")
            return false;
        PortName = portName;
        return true;
    }

    private async Task UpdateDeletedStatus(int id)
    {
        var sms = _ctx.ReceivedSms.FirstOrDefault(x => x.Id.Equals(id));
        if (sms != null) sms.Deleted = true;
        await _ctx.SaveChangesAsync();
    }
}