namespace NOF.Sample;

public class ConfigNode : AggregateRoot
{
    public ConfigNodeId Id { get; init; }
    public ConfigNodeId? ParentId { get; private set; }
    public ConfigNodeName Name { get; private set; }
    public ConfigFileName? ActiveFileName { get; private set; }

    private readonly List<ConfigFile> _configFiles = [];
    public IReadOnlyList<ConfigFileSnapshot> ConfigFiles => _configFiles.Select(c => c.ToSnapshot()).ToList().AsReadOnly();

    private ConfigNode() { }

    public static ConfigNode Create(ConfigNodeName name, ConfigNodeId? parentId)
    {
        var node = new ConfigNode
        {
            Id = ConfigNodeId.New(),
            Name = name,
            ParentId = parentId
        };

        node.AddEvent(new ConfigNodeCreatedEvent(node.Id, node.Name, node.ParentId));
        return node;
    }

    public void SetActiveFileName(ConfigFileName? fileName)
    {
        ActiveFileName = fileName;
        AddEvent(new ConfigNodeUpdatedEvent(Id, Name, ParentId));
    }

    public void AddOrUpdateConfigFile(ConfigFileName name, ConfigContent content)
    {
        var existing = _configFiles.FirstOrDefault(f => f.Name == name);
        if (existing is not null)
        {
            existing.UpdateContent(content);
        }
        else
        {
            _configFiles.Add(new ConfigFile(name, content));
        }

        AddEvent(new ConfigNodeUpdatedEvent(Id, Name, ParentId));
    }

    public void RemoveConfigFile(ConfigFileName name)
    {
        var existing = _configFiles.FirstOrDefault(f => f.Name == name);
        if (existing is not null)
        {
            _configFiles.Remove(existing);
            if (ActiveFileName == name)
            {
                ActiveFileName = null;
            }
            AddEvent(new ConfigNodeUpdatedEvent(Id, Name, ParentId));
        }
    }

    public void MarkAsDeleted()
    {
        AddEvent(new ConfigNodeDeletedEvent(Id, ParentId));
    }

    public void UpdateParent(ConfigNodeId? newParentId)
    {
        var oldParentId = ParentId;

        if (oldParentId == newParentId)
        {
            return;
        }

        ParentId = newParentId;
        AddEvent(new ConfigNodeParentUpdatedEvent(Id, oldParentId, newParentId));
    }
}

