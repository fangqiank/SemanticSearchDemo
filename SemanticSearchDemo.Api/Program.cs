using Dapper;
using Pgvector.Dapper;
using Scalar.AspNetCore;
using SemanticSearchDemo.Api.Endpoints;
using SemanticSearchDemo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

SqlMapper.AddTypeHandler(new VectorTypeHandler());

builder.Services.AddHttpClient<EmbeddingService>();

builder.Services.AddScoped<SearchService>();

var app = builder.Build();

app.UseExceptionHandler(builder =>
{
    builder.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "An internal server error occurred." });
    });
});

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapSearchEndpoints();

app.Run();
