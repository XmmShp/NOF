using NOF.Application;
using NOF.Contract;

namespace NOF.Sample.Application.RequestHandlers;

public class UpdateConfigNodeParent : IRequestHandler<UpdateConfigNodeParentRequest>
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public UpdateConfigNodeParent(IConfigNodeRepository configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(UpdateConfigNodeParentRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.NodeId);
        var node = await _configNodeRepository.FindAsync(nodeId, cancellationToken);

        if (node is null)
        {
            return Result.Fail(404, "节点不存在");
        }

        var newParentId = request.NewParentId.HasValue
            ? ConfigNodeId.Of(request.NewParentId.Value)
            : (ConfigNodeId?)null;

        // 检查新父节点是否存在（如果不是设为根节点）
        if (newParentId.HasValue)
        {
            var parentNode = await _configNodeRepository.FindAsync(newParentId.Value, cancellationToken);
            if (parentNode is null)
            {
                return Result.Fail(404, "目标父节点不存在");
            }

            // 防止循环引用：检查新父节点是否是当前节点的子孙节点
            if (await IsDescendant(nodeId, newParentId.Value, cancellationToken))
            {
                return Result.Fail(400, "不能将节点移动到其子节点下");
            }
        }

        node.UpdateParent(newParentId);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> IsDescendant(ConfigNodeId ancestorId, ConfigNodeId nodeId, CancellationToken cancellationToken)
    {
        var current = await _configNodeRepository.FindAsync(nodeId, cancellationToken);

        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
            {
                return true;
            }
            current = await _configNodeRepository.FindAsync(current.ParentId.Value, cancellationToken);
        }

        return false;
    }
}
