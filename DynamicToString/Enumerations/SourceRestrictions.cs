using System;

namespace DynamicToString.Enumerations
{
    [Flags]
    public enum SourceRestrictions
    {
        AutoMethod = MethodSource.AutoMethod | MethodSource.Custom,
        DeclaringClassMethod = AutoMethod | MethodSource.DeclaringClassMethod,
        None = DeclaringClassMethod | MethodSource.ParentClassMethod
    }
}
