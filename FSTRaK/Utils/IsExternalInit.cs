// Polyfill required for C# 9 record types on .NET Framework 4.7.2
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
