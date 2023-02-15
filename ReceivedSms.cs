using System.ComponentModel.DataAnnotations;

namespace SmsSender;

public class ReceivedSms
{
    [Key] public int Id { get; set; }

    public string? Sender { get; set; }

    public DateTimeOffset? ReceivedDate { get; set; }
    public string? Text { get; set; }

    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    
    public bool Deleted { get; set; }
}