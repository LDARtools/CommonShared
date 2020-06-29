using System;

namespace Ldartools.Common.DI
{
    [AttributeUsage(AttributeTargets.Class)]
    public class Singleton : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DontInject : Attribute
    {
    }
}
