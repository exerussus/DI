namespace Exerussus.DI
{
    using System;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class InjectAttribute : Attribute
    {
        
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ProvideAttribute : Attribute
    {
        
    }
}