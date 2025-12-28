using System.Reflection;
using System.Text.RegularExpressions;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Criteria for finding plugin types in assemblies
/// </summary>
public class TypeFinderCriteria
{
    /// <summary>
    /// Custom query function for matching types
    /// </summary>
    public Func<Type, bool>? Query { get; set; }

    /// <summary>
    /// Name pattern to match (supports wildcards * and ?)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Type that the plugin must inherit from
    /// </summary>
    public Type? Inherits { get; set; }

    /// <summary>
    /// Interface that the plugin must implement
    /// </summary>
    public Type? Implements { get; set; }

    /// <summary>
    /// Type that the plugin must be assignable to
    /// </summary>
    public Type? AssignableTo { get; set; }

    /// <summary>
    /// Attribute that the plugin must have
    /// </summary>
    public Type? HasAttribute { get; set; }

    /// <summary>
    /// Whether to match abstract types
    /// </summary>
    public bool? IsAbstract { get; set; }

    /// <summary>
    /// Whether to match interface types
    /// </summary>
    public bool? IsInterface { get; set; }

    /// <summary>
    /// Tags to apply to matched plugins
    /// </summary>
    public List<string> Tags { get; set; } = [];

    public bool IsMatch(Type type)
    {
        if (Query != null)
        {
            return Query(type);
        }

        if (IsAbstract.HasValue && type.IsAbstract != IsAbstract.Value)
        {
            return false;
        }

        if (IsInterface.HasValue && type.IsInterface != IsInterface.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Name))
        {
            var regex = NameToRegex(Name);
            var fullName = type.FullName ?? type.Name;

            if (!regex.IsMatch(fullName))
            {
                var hasDirectMatch = string.Equals(Name, type.Name, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(Name, fullName, StringComparison.OrdinalIgnoreCase);
                if (!hasDirectMatch)
                {
                    return false;
                }
            }
        }

        if (Inherits != null && !Inherits.IsAssignableFrom(type))
        {
            return false;
        }

        if (Implements != null && !Implements.IsAssignableFrom(type))
        {
            return false;
        }

        if (AssignableTo != null && !AssignableTo.IsAssignableFrom(type))
        {
            return false;
        }

        if (HasAttribute != null)
        {
            var attributes = type.GetCustomAttributesData();
            var hasAttribute = attributes.Any(a =>
                string.Equals(a.AttributeType.FullName, HasAttribute.FullName, StringComparison.OrdinalIgnoreCase));

            if (!hasAttribute)
            {
                return false;
            }
        }

        return true;
    }

    private static Regex NameToRegex(string nameFilter)
    {
        var regex = "^" + Regex.Escape(nameFilter).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        return new Regex(regex, RegexOptions.Compiled);
    }
}

/// <summary>
/// Builder for creating TypeFinderCriteria
/// </summary>
public class TypeFinderCriteriaBuilder
{
    private readonly TypeFinderCriteria _criteria = new();

    public static TypeFinderCriteriaBuilder Create() => new();

    public TypeFinderCriteriaBuilder Name(string name)
    {
        _criteria.Name = name;
        return this;
    }

    public TypeFinderCriteriaBuilder Inherits<T>() where T : class
    {
        _criteria.Inherits = typeof(T);
        return this;
    }

    public TypeFinderCriteriaBuilder Inherits(Type type)
    {
        _criteria.Inherits = type;
        return this;
    }

    public TypeFinderCriteriaBuilder Implements<T>() where T : class
    {
        _criteria.Implements = typeof(T);
        return this;
    }

    public TypeFinderCriteriaBuilder Implements(Type type)
    {
        _criteria.Implements = type;
        return this;
    }

    public TypeFinderCriteriaBuilder AssignableTo<T>() where T : class
    {
        _criteria.AssignableTo = typeof(T);
        return this;
    }

    public TypeFinderCriteriaBuilder AssignableTo(Type type)
    {
        _criteria.AssignableTo = type;
        return this;
    }

    public TypeFinderCriteriaBuilder HasAttribute<T>() where T : Attribute
    {
        _criteria.HasAttribute = typeof(T);
        return this;
    }

    public TypeFinderCriteriaBuilder HasAttribute(Type type)
    {
        _criteria.HasAttribute = type;
        return this;
    }

    public TypeFinderCriteriaBuilder IsAbstract(bool isAbstract = true)
    {
        _criteria.IsAbstract = isAbstract;
        return this;
    }

    public TypeFinderCriteriaBuilder IsInterface(bool isInterface = true)
    {
        _criteria.IsInterface = isInterface;
        return this;
    }

    public TypeFinderCriteriaBuilder Tag(string tag)
    {
        _criteria.Tags.Add(tag);
        return this;
    }

    public TypeFinderCriteriaBuilder Query(Func<Type, bool> query)
    {
        _criteria.Query = query;
        return this;
    }

    public TypeFinderCriteria Build() => _criteria;
}
