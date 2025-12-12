using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithImage("masstransit/rabbitmq", "latest")
    .WithHttpEndpoint(targetPort: 15672, name: "RabbitMQManagePlugin");

var garnet = builder.AddGarnet("garnet");

var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("db");

var sample = builder.AddProject<NOF_Sample>("NOF-Sample")
    .WithReference(rabbitMq, "rabbitmq")
    .WaitFor(rabbitMq)
    .WithReference(database, "postgres")
    .WaitFor(database)
    .WithReference(garnet, "redis")
    .WaitFor(garnet);

builder.Build().Run();
