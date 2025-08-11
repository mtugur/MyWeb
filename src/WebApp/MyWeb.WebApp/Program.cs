using System.Text;
using MyWeb.Communication.Siemens;
using MyWeb.Core.Communication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// UTF-16, CP1254 vs. kod sayfaları için
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// PLC bağlantı ayarlarını oku
builder.Services.Configure<PlcConnectionSettings>(
    builder.Configuration.GetSection("PlcConnectionSettings"));

// ICommunicationChannel olarak SiemensCommunicationChannel kaydı
builder.Services.AddSingleton<ICommunicationChannel>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<PlcConnectionSettings>>().Value;
    var channel = new SiemensCommunicationChannel(settings);

    // DB1000 üzerindeki tag tanımları
    channel.AddTag(new TagDefinition { Name = "tBool", Address = "DB1000.DBX0.0", DataType = "DataBlock", VarType = "Bit", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tByte", Address = "DB1000.DBB1", DataType = "DataBlock", VarType = "Byte", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tWord", Address = "DB1000.DBW2", DataType = "DataBlock", VarType = "Word", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tLWord", Address = "DB1000.DBD4", DataType = "DataBlock", VarType = "LWord", Count = 2 });
    channel.AddTag(new TagDefinition { Name = "tDWord", Address = "DB1000.DBD12", DataType = "DataBlock", VarType = "DWord", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tInt", Address = "DB1000.DBW16", DataType = "DataBlock", VarType = "Int", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tSInt", Address = "DB1000.DBB18", DataType = "DataBlock", VarType = "SInt", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tUInt", Address = "DB1000.DBW20", DataType = "DataBlock", VarType = "Word", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tULInt", Address = "DB1000.DBD22", DataType = "DataBlock", VarType = "ULInt", Count = 2 });
    channel.AddTag(new TagDefinition { Name = "tDInt", Address = "DB1000.DBD30", DataType = "DataBlock", VarType = "DInt", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tLInt", Address = "DB1000.DBD34", DataType = "DataBlock", VarType = "LInt", Count = 2 });
    channel.AddTag(new TagDefinition { Name = "tReal", Address = "DB1000.DBD42", DataType = "DataBlock", VarType = "Real", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tLReal", Address = "DB1000.DBD46", DataType = "DataBlock", VarType = "LReal", Count = 1 });
    channel.AddTag(new TagDefinition { Name = "tString", Address = "DB1000.DBB54", DataType = "DataBlock", VarType = "String", Count = 20 });
    channel.AddTag(new TagDefinition { Name = "tWString", Address = "DB1000.DBB76", DataType = "DataBlock", VarType = "WString", Count = 20 });

    return channel;
});

// MVC / API
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
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
