using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDbContextModelCreatingContributor
{
    void Configure(IDbModelBuilder modelBuilder);
}
