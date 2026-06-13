// netstandard2.1 には IsExternalInit が無く record struct の init アクセサが使えないため補う
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
