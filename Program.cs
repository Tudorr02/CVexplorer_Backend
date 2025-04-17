using CVexplorer.Data;
using CVexplorer.Extensions;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityService(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(builder => builder
                .WithOrigins("http://localhost:4205", "https://localhost:4205") // Only allow specific origins
                .AllowAnyMethod() // Allow all HTTP methods (GET, POST, etc.)
                .AllowAnyHeader() // Allow all headers
                .AllowCredentials() // Allow cookies or credentials
            );

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.MapControllers();

app.Run();


