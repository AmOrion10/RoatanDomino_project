namespace DominoCommon.com.domino.common.Scripts.Enums
{
    public enum TeamRole
    {
        Teammate = 0,
        ViceCaptain = 1,
        Captain = 2
    }

    public enum TeamActivityRequirement
    {
        Any = 0,
        TwiceWeekly = 1,
        Daily = 2
    }

    public enum TeamJoinPolicy
    {
        Open = 0,
        ApprovalRequired = 1
    }

    public static class TeamEnumHelper
    {
        public static string GetDisplayName(this TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Captain:
                    return "Captain";
                case TeamRole.ViceCaptain:
                    return "Vice-Captain";
                default:
                    return "Teammate";
            }
        }

        public static string GetBadgeColorHex(this TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Captain:
                    return "#F7C948";
                case TeamRole.ViceCaptain:
                    return "#38BDF8";
                default:
                    return "#9FD8CB";
            }
        }

        public static string GetDisplayName(this TeamActivityRequirement req)
        {
            switch (req)
            {
                case TeamActivityRequirement.TwiceWeekly:
                    return "Twice Weekly (2+/7d)";
                case TeamActivityRequirement.Daily:
                    return "Daily (24h)";
                default:
                    return "Any Activity";
            }
        }

        public static string GetDisplayName(this TeamJoinPolicy policy)
        {
            switch (policy)
            {
                case TeamJoinPolicy.ApprovalRequired:
                    return "Approval Required";
                default:
                    return "Open";
            }
        }
    }
}
