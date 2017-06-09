using System;

namespace DynamicToString.Enumerations
{
    [Flags]
    public enum MethodSource
    {
        Custom = 1 << 0,
        AutoMethod = 1 << 2,
        ClassMethod = 1 << 3,
        DeclaringClassMethod = ClassMethod | (1 << 4),
        ParentClassMethod = ClassMethod | (1 << 5),

        Invalid = int.MaxValue

    }
}
