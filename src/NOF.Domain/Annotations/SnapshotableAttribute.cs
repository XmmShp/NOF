namespace NOF.Domain;

/// <summary>
/// Marks a class for snapshot class generation by the source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class SnapshotableAttribute : Attribute;

/// <summary>
/// Marks a property to be excluded from snapshot generation.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SnapshotIgnoreAttribute : Attribute;
