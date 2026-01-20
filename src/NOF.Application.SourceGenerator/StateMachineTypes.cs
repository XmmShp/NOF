using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace NOF.Application.SourceGenerator;

public class StateMachineDefinitionInfo
{
    public INamedTypeSymbol Symbol { get; set; }
    public INamedTypeSymbol StateType { get; set; }
    public INamedTypeSymbol ContextType { get; set; }
    public IMethodSymbol BuildMethod { get; set; }
    public string ClassName { get; set; }
    public string Namespace { get; set; }
}

public class StateMachineLogic
{
    public List<string> Members { get; } = new();
}

public class CorrelationConfig
{
    public string NotificationType { get; set; }
    public string Expression { get; set; }
    public string ParameterName { get; set; }
}

public class StartConfig
{
    public string NotificationType { get; set; }
    public string State { get; set; }
    public List<ContextProperty> ContextProperties { get; set; } = new();
    public List<string> Actions { get; set; } = new();
}

public class TransferConfig
{
    public string State { get; set; }
    public List<NotificationConfig> NotificationConfigs { get; set; } = new();
}

public class NotificationConfig
{
    public string NotificationType { get; set; }
    public string TargetState { get; set; }
    public List<string> Actions { get; set; } = new();
}

public class ContextProperty
{
    public string Name { get; set; }
    public string Value { get; set; }
}
