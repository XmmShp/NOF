namespace NOF;

/// <summary>
/// 标记一个类用于自动生成ToQueryString扩展方法，将类的属性转换为URL查询参数
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class QueryParameterAttribute : Attribute;
