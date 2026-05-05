using Innova.Wifi.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);


builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Innova WiFi Agent";
});


builder.Services.AddHostedService<Worker>();
builder.Services.AddHttpClient<WifiRepository>();
var host = builder.Build();
host.Run();