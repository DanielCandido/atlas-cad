var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddPolicy("LocalWeb", p => p.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors("LocalWeb");
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "atlas-licensing-api" }));
app.MapGet("/api/products", () => Results.Ok(new[] { new { id = "atlas-mline-pro", name = "ATLAS MLINE PRO", status = "coming-soon" } }));
app.MapGet("/api/licenses/demo-status", () => Results.Ok(new { demo = true, licensed = false, message = "Endpoint demonstrativo; não autoriza execução." }));
app.Run();
