namespace Domain;

public class FriendRequest
{
    public long UserIdAsked { get; set; }
    public string UserAskedName { get; set; }

    public long UserIdApplicant { get; set; }
    public string UserApplicantName { get; set; }
}
