using System.Security.Claims;

namespace NOF.Abstraction;

/// <summary>
/// 可观察的用户主体，仅允许追加 <see cref="ClaimsIdentity"/>，退出登录由 <see cref="UserContext"/> 通过替换实例统一管理。
/// </summary>
public class UserClaimsPrincipal : ClaimsPrincipal
{
    private readonly Action _stateChanging;
    private readonly Action _stateChanged;

    public UserClaimsPrincipal(Action stateChanging, Action stateChanged)
    {
        _stateChanging = stateChanging;
        _stateChanged = stateChanged;
    }

    /// <inheritdoc />
    public override void AddIdentity(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        _stateChanging();
        base.AddIdentity(identity);
        _stateChanged();
    }

    /// <inheritdoc />
    public override void AddIdentities(IEnumerable<ClaimsIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);

        var identityList = identities.ToList();
        if (identityList.Count == 0)
        {
            return;
        }

        _stateChanging();
        base.AddIdentities(identityList);
        _stateChanged();
    }
}
