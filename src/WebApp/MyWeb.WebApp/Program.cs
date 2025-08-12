using System.Text;
using Microsoft.Extensions.Options;
using MyWeb.Core.Communication;
using MyWeb.Communication.Siemens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// STRING (CP1254) vb. için gerekli kod sayfaları
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Serilog yapılandırması
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/app-.log",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7,
                  fileSizeLimitBytes: 10_000_000,
                  rollOnFileSizeLimit: true)
    .CreateLogger();

builder.Host.UseSerilog(Log.Logger);

// PLC bağlantı ayarlarını oku
builder.Services.Configure<PlcConnectionSettings>(
    builder.Configuration.GetSection("PlcConnectionSettings"));

// Siemens kanalını DI ile kaydet (ILogger ve IOptions ctor’a enjekte edilecek)
builder.Services.AddSingleton<ICommunicationChannel>(sp =>
{
    var channel = new SiemensCommunicationChannel(
        sp.GetRequiredService<IOptions<PlcConnectionSettings>>(),
        sp.GetRequiredService<ILogger<SiemensCommunicationChannel>>()
    );

    // DB1000 üzerindeki tag tanımları (DB/JSON gelene kadar burada dursun)
    channel.AddTag(new TagDefinition { Name = "tBool", Address = "DB1000.DBX0.0", DataType = "DataBlock", VarType = "Bit", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tByte", Address = "DB1000.DBB1", DataType = "DataBlock", VarType = "Byte", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tWord", Address = "DB1000.DBW2", DataType = "DataBlock", VarType = "Word", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tLWord", Address = "DB1000.DBD4", DataType = "DataBlock", VarType = "LWord", Count = 2 }); // 8B
    channel.AddTag(new TagDefinition { Name = "tDWord", Address = "DB1000.DBD12", DataType = "DataBlock", VarType = "DWord", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tInt", Address = "DB1000.DBW16", DataType = "DataBlock", VarType = "Int", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tSInt", Address = "DB1000.DBB18", DataType = "DataBlock", VarType = "SInt", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tUInt", Address = "DB1000.DBW20", DataType = "DataBlock", VarType = "Word", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tULInt", Address = "DB1000.DBD22", DataType = "DataBlock", VarType = "ULInt", Count = 2 }); // 8B
    channel.AddTag(new TagDefinition { Name = "tDInt", Address = "DB1000.DBD30", DataType = "DataBlock", VarType = "DInt", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tLInt", Address = "DB1000.DBD34", DataType = "DataBlock", VarType = "LInt", Count = 2 }); // 8B
    channel.AddTag(new TagDefinition { Name = "tReal", Address = "DB1000.DBD42", DataType = "DataBlock", VarType = "Real", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tLReal", Address = "DB1000.DBD46", DataType = "DataBlock", VarType = "LReal", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tString", Address = "DB1000.DBB54", DataType = "DataBlock", VarType = "String", Count = 20 });
    channel.AddTag(new TagDefinition { Name = "tWString", Address = "DB1000.DBB76", DataType = "DataBlock", VarType = "WString", Count = 20 });

    // Başlangıçta bağlan
    channel.Connect();
    return channel;
});

// MVC / API
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging(); // istek logları

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
).WithStaticAssets();

app.Run();
