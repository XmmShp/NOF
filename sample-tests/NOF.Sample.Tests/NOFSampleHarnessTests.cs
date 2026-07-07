using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using NOF.Sample;
using NOF.Sample.Application;
using NOF.Sample.Application.Entities;
using NOF.Test;
using Xunit;

namespace NOF.Sample.Tests;

public sealed class NOFSampleHarnessTests
{
    [Fact]
    public async Task LocalRpcHarness_ShouldCreateRootNode_AndQueryItBack()
    {
        await using var host = await CreateHostAsync();

        var createResult = await host.CallAsync<INOFSampleServiceClient, Result>(
            static (client, context, cancellationToken) => client.CreateConfigNodeAsync(
                new CreateConfigNodeRequest { Name = "root-a" },
                context,
                cancellationToken),
            configure: scope => scope
                .SetTenant("tenant-a")
                .SetUser("user-1", "Alice"));

        Assert.True(createResult.IsSuccess);

        var roots = await host.CallAsync<INOFSampleServiceClient, Result<GetRootConfigNodesResponse>>(
            static (client, context, cancellationToken) => client.GetRootConfigNodesAsync(
                new GetRootConfigNodesRequest(),
                context,
                cancellationToken),
            configure: scope => scope
                .SetTenant("tenant-a")
                .SetUser("user-1", "Alice"));

        Assert.True(roots.IsSuccess);
        var root = Assert.Single(roots.Value!.Nodes);
        Assert.Equal("root-a", root.Name);
        Assert.Null(root.ParentId);
    }

    [Fact]
    public async Task LocalRpcHarness_ShouldPersistTenantData_AndTriggerInMemoryProjection()
    {
        await using var host = await CreateHostAsync();

        var parentId = await host.ExecuteAsync(async scope =>
        {
            scope.SetTenant("tenant-b")
                .SetUser("user-2", "Bob");

            var createParent = await scope.CallAsync<INOFSampleServiceClient, Result>(
                (client, context, cancellationToken) => client.CreateConfigNodeAsync(
                    new CreateConfigNodeRequest { Name = "parent-b" },
                    context,
                    cancellationToken));
            Assert.True(createParent.IsSuccess);

            var roots = await scope.CallAsync<INOFSampleServiceClient, Result<GetRootConfigNodesResponse>>(
                (client, context, cancellationToken) => client.GetRootConfigNodesAsync(
                    new GetRootConfigNodesRequest(),
                    context,
                    cancellationToken));
            var root = Assert.Single(roots.Value!.Nodes);

            var createChild = await scope.CallAsync<INOFSampleServiceClient, Result>(
                (client, context, cancellationToken) => client.CreateConfigNodeAsync(
                    new CreateConfigNodeRequest { Name = "child-b", ParentId = root.Id },
                    context,
                    cancellationToken));
            Assert.True(createChild.IsSuccess);

            return root.Id;
        });

        await host.ExecuteAsync(async scope =>
        {
            scope.SetTenant("tenant-b")
                .SetUser("user-2", "Bob");

            var parent = await scope.CallAsync<INOFSampleServiceClient, Result<GetConfigNodeByIdResponse>>(
                (client, context, cancellationToken) => client.GetConfigNodeByIdAsync(
                    new GetConfigNodeByIdRequest { Id = parentId },
                    context,
                    cancellationToken));

            Assert.True(parent.IsSuccess);
            Assert.Equal("parent-b", parent.Value!.Node.Name);

            var projection = await scope.GetRequiredService<IDbContext>()
                .Set<ConfigNodeChildren>()
                .FirstOrDefaultAsync(entity => entity.NodeId == ConfigNodeId.Of(parentId));

            Assert.NotNull(projection);
            Assert.Single(projection.ChildrenIds);
        });
    }

    private static async Task<NOFTestHost> CreateHostAsync()
    {
        var builder = NOFTestAppBuilder.Create()
            .AddApplicationPartOf<NOFSampleService>()
            .AddApplicationPartOf<LocalSampleServiceClient>()
            .AddRpcServer<NOFSampleService>()
            .AddInMemoryPersistence()
            .AddLocalRpcClient<INOFSampleServiceClient, LocalSampleServiceClient>();

        return await builder.BuildTestHostAsync();
    }
}

[LocalRpcClient<INOFSampleServiceClient>]
public sealed partial class LocalSampleServiceClient : INOFSampleServiceClient;

[Mappable<ConfigFile, ConfigFileDto>]
[Mappable<ConfigNode, ConfigNodeDto>]
public static partial class SampleHarnessMappings;
