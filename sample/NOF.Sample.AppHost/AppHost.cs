using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitMqUserName = builder.AddParameter("rabbitMqUserName", "guest");
var rabbitMqPassword = builder.AddParameter("rabbitMqPassword", "guest");
var rabbitMq = builder.AddRabbitMQ("rabbitmq", rabbitMqUserName, rabbitMqPassword)
    .WithImage("masstransit/rabbitmq", "latest")
    .WithHttpEndpoint(targetPort: 15672, name: "RabbitMQManagePlugin")
    .WithLifetime(ContainerLifetime.Persistent);
rabbitMqUserName.WithParentRelationship(rabbitMq);
rabbitMqPassword.WithParentRelationship(rabbitMq);

var garnet = builder.AddGarnet("garnet")
    .WithLifetime(ContainerLifetime.Persistent);

var postgresUserName = builder.AddParameter("postgresUserName", "postgres");
var postgresPassword = builder.AddParameter("postgresPassword", "123456");
var postgres = builder.AddPostgres("postgres", postgresUserName, postgresPassword)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent)
    .PublishAsConnectionString();
postgresUserName.WithParentRelationship(postgres);
postgresPassword.WithParentRelationship(postgres);
var database = postgres.AddDatabase("db");

var sample = builder.AddProject<NOF_Sample_Hosting>("NOF-Sample")
    .WithReference(rabbitMq, "rabbitmq")
    .WaitFor(rabbitMq)
    .WithReference(database, "postgres")
    .WaitFor(database)
    .WithReference(garnet, "redis")
    .WaitFor(garnet);

builder.Build().Run();
