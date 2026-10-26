namespace Nuskha.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Editor = "Editor";
    public const string User = "User";

    /// <summary>Roles that can be assigned from the admin panel.</summary>
    public static readonly string[] All = [Admin, Moderator, Editor, User];
}
