using System;
using System.Reflection;

public class MWMessageProperty<DT, T> : IMWMessageProperty
{
    public string Name;
    private PropertyInfo _property;

	public MWMessageProperty(string name)
	{
        Name = name;
        _property = typeof(T).GetProperty(name, typeof(DT));
        if (_property == null)
        {
            throw new InvalidOperationException(String.Format("No property {0} in class {1}", name, typeof(T)));
        }
        else if (_property.SetMethod == null || _property.GetMethod == null)
        {
            throw new InvalidOperationException(String.Format("Property {0} in class {1} needs publicly accessible getter and setter", name, typeof(T)));
        }
	}

    public void Set(object target, object val)
    {
        _property.SetValue(target, val, null);
    }

    public object Get(object target)
    {
        return _property.GetValue(target);
    }
}
