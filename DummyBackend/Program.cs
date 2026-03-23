var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
});
var app = builder.Build();

app.MapGet("/", () => "OK");
app.MapGet("/{**catch-all}", () => "OK");

app.Run();
