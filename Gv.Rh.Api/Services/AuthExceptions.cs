namespace Gv.Rh.Api.Services;

public sealed class MustChangePasswordException : Exception
{
    public MustChangePasswordException() : base("MUST_CHANGE_PASSWORD") { }
}