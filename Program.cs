using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmsSender;
using SmsSender.EntityFramework;

var serviceProvider = new ServiceCollection()
    .AddDbContext<AppDbContext>(options =>
        options.UseSqlServer("DefaultConnection"))
    .BuildServiceProvider();

var app = ActivatorUtilities.CreateInstance<SmsRepository>(serviceProvider);
await app.Run();


Console.ReadLine();