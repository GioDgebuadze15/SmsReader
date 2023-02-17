using System.Management;
using System.Text.RegularExpressions;

namespace SmsSender;

public class UsbSerialConverterInfo
{
    public string DeviceId { get; set; }
    public string Description { get; set; }

    public string Name { get; set; }
    public string Manufacturer { get; set; }

    private UsbSerialConverterInfo(string deviceId, string description, string name,
        string manufacturer)
    {
        DeviceId = deviceId;
        Description = description;
        Name = name;
        Manufacturer = manufacturer;
    }

    public static string? GetPortName()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Console.WriteLine("This program must be run from windows!");
            return null;
        }


        var converters = new List<UsbSerialConverterInfo>();

        ManagementObjectCollection collection;
        using (var searcher =
               new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE Manufacturer = 'FTDI'"))
            collection = searcher.Get();

        foreach (var device in collection)
        {
            try
            {
                converters.Add(new UsbSerialConverterInfo(
                    device.GetPropertyValue("DeviceId").ToString(),
                    device.GetPropertyValue("Description").ToString(),
                    device.GetPropertyValue("Name").ToString(),
                    device.GetPropertyValue("Manufacturer").ToString()
                ));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        collection.Dispose();

        var usbConverter = converters.FirstOrDefault(x => x.Name.Contains("COM"));
        if (usbConverter == null) return null;

        var regex = new Regex(@"(?<comPort>COM\d+)", RegexOptions.Compiled);
        var portName = regex.Match(usbConverter.Name).Groups["comPort"].Value.Trim();

        Console.WriteLine($"Device {usbConverter.DeviceId} is connected to COM port {portName}");


        return portName;
    }
}