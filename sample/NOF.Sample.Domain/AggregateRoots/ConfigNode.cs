using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Sample;

public class ConfigNode
{
    public ConfigNodeId Id { get; init; }
    public ConfigNodeId? ParentId { get; private set; }
    public ConfigNodeName Name { get; private set; }
    public ConfigFileName? ActiveFileName { get; private set; }

    public IEnumerable<ConfigFile> ConfigFiles { get; } = new List<ConfigFile>();

    private ConfigNode() { }

    public static ConfigNode Create(ConfigNodeName name, ConfigNodeId? parentId)
    {
        var node = new ConfigNode
        {
            Id = ConfigNodeId.New(),
            Name = name,
            ParentId = parentId
        };

        new ConfigNodeCreatedEvent(node.Id, node.Name, node.ParentId).PublishAsEvent();
        return node;
    }

    public void SetActiveFileName(ConfigFileName? fileName)
    {
        ActiveFileName = fileName;
        new ConfigNodeUpdatedEvent(Id, Name, ParentId).PublishAsEvent();
    }

    public void AddOrUpdateConfigFile(ConfigFileName name, ConfigContent content)
    {
        var existing = ConfigFiles.FirstOrDefault(f => f.Name == name);
        if (existing is not null)
        {
            existing.UpdateContent(content);
        }
        else
        {
            ConfigFiles.Mut.Add(new ConfigFile(name, content));
        }

        new ConfigNodeUpdatedEvent(Id, Name, ParentId).PublishAsEvent();
    }

    public void RemoveConfigFile(ConfigFileName name)
    {
        var existing = ConfigFiles.FirstOrDefault(f => f.Name == name);
        if (existing is not null)
        {
            ConfigFiles.Mut.Remove(existing);
            if (ActiveFileName == name)
            {
                ActiveFileName = null;
            }
            new ConfigNodeUpdatedEvent(Id, Name, ParentId).PublishAsEvent();
        }
    }

    public void MarkAsDeleted()
    {
        new ConfigNodeDeletedEvent(Id, ParentId).PublishAsEvent();
    }

    public void UpdateParent(ConfigNodeId? newParentId)
    {
        var oldParentId = ParentId;

        if (oldParentId == newParentId)
        {
            return;
        }

        ParentId = newParentId;
        new ConfigNodeParentUpdatedEvent(Id, oldParentId, newParentId).PublishAsEvent();
    }
}
