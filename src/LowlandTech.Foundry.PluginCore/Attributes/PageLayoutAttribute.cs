namespace LowlandTech.Foundry.PluginCore.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PageLayoutAttribute : Attribute
{
    public Type LayoutType { get; }

    public PageLayoutAttribute(Type layoutType)
    {
        if (!typeof(Microsoft.AspNetCore.Components.LayoutComponentBase).IsAssignableFrom(layoutType))
        {
            throw new ArgumentException($"Layout type must inherit from LayoutComponentBase", nameof(layoutType));
        }
        LayoutType = layoutType;
    }
}
