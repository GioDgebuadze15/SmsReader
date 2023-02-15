using Microsoft.EntityFrameworkCore;

namespace SmsSender.EntityFramework;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlServer(
            "data source=DESKTOP-HTKI2NS\\SQLEXPRESS;Initial Catalog=SmsDb;Trusted_Connection=True;Trust Server Certificate=true;");

    public DbSet<ReceivedSms> ReceivedSms { get; set; }
}