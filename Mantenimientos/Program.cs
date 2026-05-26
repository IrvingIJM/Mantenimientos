using Mantenimientos.Data;
using Mantenimientos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

//Servicios VMC
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MttosConnection"),
    sqlOptions => sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 3,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorNumbersToAdd: null)
    )
);

//Servicios de la base de datos
builder.Services.AddScoped<EmpDataService>();

//Logging
builder.Services.AddLogging();
var app = builder.Build();

//Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

//Ruta por defecto
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Seguimiento}/{action=Index}/{id?}");

//Aplicar migraciones automaticamente al iniciar la aplicación
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}
app.Run();

