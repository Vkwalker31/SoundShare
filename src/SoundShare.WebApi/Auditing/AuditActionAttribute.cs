namespace SoundShare.WebApi.Auditing;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class AuditActionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
