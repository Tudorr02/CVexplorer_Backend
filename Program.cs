using CVexplorer.Extensions;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityService(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(builder => builder
                .WithOrigins("http://localhost:4205", "https://localhost:4205") // Only allow specific origins
                .AllowAnyMethod() // Allow all HTTP methods (GET, POST, etc.)
                .AllowAnyHeader() // Allow all headers
                .AllowCredentials() // Allow cookies or credentials
            );

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


