using System;

public interface IMWMessageProperty
{
    void SetValue(object target, object val);
    object GetValue(object target);
}
