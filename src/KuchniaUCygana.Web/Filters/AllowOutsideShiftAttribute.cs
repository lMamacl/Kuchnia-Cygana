namespace KuchniaUCygana.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowOutsideShiftAttribute : Attribute
{
}
