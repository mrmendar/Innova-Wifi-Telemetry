using Innova.Wifi.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// KRÝTÝK EKLEME: Bu satýr, uygulamanýn Windows Servisleri ile haberleþmesini saðlar.
// Paket kurulu deðilse: dotnet add package Microsoft.Extensions.Hosting.WindowsServices
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Innova WiFi Agent";
});

// Senin mevcut 'Worker' sýnýfýný kullanmaya devam ediyoruz.
// Sýnýf adýn 'Worker' olduðu için burayý deðiþtirmiyorum.
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();