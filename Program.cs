using BackSolutions.Externals.ExternalServices;
using BackSolutions.Externals.Interfaces;
using BackSolutions.Services.interfaces;
using BackSolutions.Services.InternalServices;
using BackSolutions.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
// obtener los valores de configuración de HuggingFace desde appsettings.json
builder.Services.Configure<HFSettings>(builder.Configuration.GetSection("HuggingFace"));
// obtener los valores de configuración de Email desde appsettings.json
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));


// 2. Registrar los servicios Externos (Infraestructura)
builder.Services.AddHttpClient<InteligenceInterface, InteligenceSerivice>();
builder.Services.AddTransient<EmailInterface, EmailExternal>();

// 3. Registrar los servicios Internos (Lógica de Negocio)
builder.Services.AddTransient<GeneratePDFInterface, GenerarPDFService>();
builder.Services.AddTransient<CotizarInterface, CotizarService>();




var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();


