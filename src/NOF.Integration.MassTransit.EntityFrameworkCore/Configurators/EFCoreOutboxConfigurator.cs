using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NOF;

public static class EFCoreOutboxConfigurator
{
    public static IModel BuildModel()
    {
        var builder = new ModelBuilder();
        builder.AddTransactionalOutboxEntities();
        return builder.Model.FinalizeModel();
    }
}