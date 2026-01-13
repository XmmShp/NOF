namespace NOF;

/// <summary>
/// 快照特性，用于标记需要生成快照类的类
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class SnapshotableAttribute : Attribute;

/// <summary>
/// 快照忽略特性，用于标记不需要生成快照的属性
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SnapshotIgnoreAttribute : Attribute;