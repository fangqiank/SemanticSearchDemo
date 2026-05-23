var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithGPUSupport();

var embeddingModel = ollama.AddModel("qwen3-embedding:0.6b");

var postgres = builder.AddPostgres("postgres", port: 6432)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithImage("pgvector/pgvector", "pg17")
    .AddDatabase("articles");

builder.AddProject<Projects.SemanticSearchDemo_Api>("semanticsearchdemo-api")
    .WithReference(embeddingModel)
    .WithReference(postgres)
    .WaitFor(embeddingModel)
    .WaitFor(postgres);

builder.Build().Run();
