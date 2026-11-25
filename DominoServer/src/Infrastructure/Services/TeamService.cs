using System.Text.RegularExpressions;

using Application.Interfaces;
using Application.Interfaces.Managers;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class TeamActionResult<T>
{
    public bool Ok { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Data { get; init; }

    public static TeamActionResult<T> Success(T data) => new() { Ok = true, Data = data };
    public static TeamActionResult<T> Fail(string message) => new() { Ok = false, ErrorMessage = message };
}

public class TeamService(
    ApplicationDbContext dbContext,
    IClientManager clientManager,
    ILobbyManager? lobbyManager = null) : ITeamChallengeEloUpdater
{
    public const int MaxTeamMembers = TeamInfoDto.DefaultMaxMembers;
    public const int NoShowForfeitEloDelta = 16;
    public const int MutualNoShowEloPenalty = 12;
    public const int AbandonmentTeamEloDelta = 15;

    private static readonly SemaphoreSlim OpenChallengeClaimLock = new(1, 1);
    private static readonly Regex TagPattern = new(@"^[A-Za-z0-9]{3,4}$", RegexOptions.Compiled);

    private static int ResolveMaxMembers(TeamEntity team)
    {
        return team.MaxMembers > 0 ? Math.Min(team.MaxMembers, MaxTeamMembers) : MaxTeamMembers;
    }

    public static int CalculateAverageMemberElo(TeamEntity team)
    {
        if (team.Members is null || team.Members.Count == 0)
        {
            return RoatanRankTierHelper.DefaultElo;
        }

        double avg = team.Members
            .Where(m => m.User is not null)
            .Select(m => m.User.Elo >= RoatanRankTierHelper.MinElo ? m.User.Elo : RoatanRankTierHelper.DefaultElo)
            .DefaultIfEmpty(RoatanRankTierHelper.DefaultElo)
            .Average();

        return RoatanRankTierHelper.ClampElo((int)Math.Round(avg, MidpointRounding.AwayFromZero));
    }

    public static (bool MeetsRequirements, string? DisqualifyReason) EvaluateEligibility(
        UserEntity user,
        TeamEntity team,
        int currentMemberCount,
        DateTime? nowUtc = null)
    {
        DateTime utcNow = nowUtc ?? DateTime.UtcNow;
        int maxMembers = ResolveMaxMembers(team);

        if (currentMemberCount >= maxMembers)
        {
            return (false, $"This team is full ({currentMemberCount}/{maxMembers} members).");
        }

        int userElo = user.Elo >= RoatanRankTierHelper.MinElo ? user.Elo : RoatanRankTierHelper.DefaultElo;
        int minElo = RoatanRankTierHelper.ClampElo(team.MinElo);

        string? eloRequirementText = null;
        if (userElo < minElo)
        {
            string tierName = RoatanRankTierHelper.FromElo(minElo).GetDisplayName();
            eloRequirementText = $"{tierName} rank ({minElo}+ Elo)";
        }

        string? activityRequirementText = null;
        if (team.ActivityRequirement == TeamActivityRequirement.TwiceWeekly)
        {
            if (user.OnlineMatchesLast7Days < 2)
            {
                activityRequirementText = "Twice-Weekly activity (2+ online matches in 7 days)";
            }
        }
        else if (team.ActivityRequirement == TeamActivityRequirement.Daily)
        {
            bool playedLast24h = user.LastOnlineMatchUtc.HasValue && user.LastOnlineMatchUtc.Value >= utcNow.AddHours(-24);
            if (!playedLast24h)
            {
                activityRequirementText = "Daily activity";
            }
        }

        if (eloRequirementText is not null && activityRequirementText is not null)
        {
            return (false, $"Requires {eloRequirementText} and {activityRequirementText}.");
        }
        if (eloRequirementText is not null)
        {
            return (false, $"Requires {eloRequirementText}.");
        }
        if (activityRequirementText is not null)
        {
            return (false, $"Requires {activityRequirementText}.");
        }

        return (true, null);
    }

    public async Task<TeamsListDto> GetTeamsAsync(long requestingUserId, string? searchQuery = null)
    {
        UserEntity? requestingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == requestingUserId);

        List<TeamEntity> teams = await dbContext.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .ToListAsync();

        string trimmedSearch = searchQuery?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            teams = teams
                .Where(t =>
                    t.Name.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase) ||
                    t.Tag.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        DateTime utcNow = DateTime.UtcNow;
        List<TeamInfoDto> teamDtos = teams
            .OrderByDescending(t => RoatanRankTierHelper.ClampElo(t.TeamElo))
            .ThenByDescending(t => t.TeamWins)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => BuildTeamInfoDto(t, requestingUser, utcNow))
            .ToList();

        List<TeamInviteDto> pendingInvites = await GetPendingInvitesForUserAsync(requestingUserId);

        return new TeamsListDto
        {
            Teams = teamDtos,
            PendingInvites = pendingInvites
        };
    }

    public async Task<MyTeamDetailsDto> GetMyTeamAsync(long userId, string? statusMessage = null)
    {
        TeamMemberEntity? membership = await dbContext.TeamMembers
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (membership is null)
        {
            return new MyTeamDetailsDto
            {
                TeamInfo = null,
                MyRole = null,
                Members = [],
                PendingRequests = [],
                PendingInvites = await GetPendingInvitesForUserAsync(userId),
                StatusMessage = statusMessage
            };
        }

        TeamEntity? team = await dbContext.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
                    .ThenInclude(u => u.Avatar)
            .Include(t => t.JoinRequests)
                .ThenInclude(r => r.User)
                    .ThenInclude(u => u.Avatar)
            .FirstOrDefaultAsync(t => t.Id == membership.TeamId);

        if (team is null)
        {
            return new MyTeamDetailsDto
            {
                PendingInvites = await GetPendingInvitesForUserAsync(userId),
                StatusMessage = statusMessage
            };
        }

        HashSet<long> onlineUserIds = clientManager.GetAllLoggedInClients()
            .Where(c => c.User is not null && c.User.Id > 0)
            .Select(c => c.User!.Id)
            .ToHashSet();

        UserEntity? selfEntity = team.Members.FirstOrDefault(m => m.UserId == userId)?.User;
        TeamInfoDto teamInfo = BuildTeamInfoDto(team, selfEntity, DateTime.UtcNow);

        List<TeamMemberDto> memberDtos = team.Members
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .ThenBy(m => m.UserId)
            .Select(m =>
            {
                int memberElo = m.User.Elo >= RoatanRankTierHelper.MinElo ? m.User.Elo : RoatanRankTierHelper.DefaultElo;
                return new TeamMemberDto
                {
                    UserId = m.UserId,
                    Username = m.User.Login,
                    AvatarUrl = m.User.AvatarUrl,
                    AvatarBase64 = m.User.Avatar is not null && m.User.Avatar.Image is { Length: > 0 }
                        ? Convert.ToBase64String(m.User.Avatar.Image)
                        : null,
                    Elo = memberElo,
                    RankTier = RoatanRankTierHelper.FromElo(memberElo),
                    Role = m.Role,
                    IsOnline = onlineUserIds.Contains(m.UserId),
                    JoinedAtUtc = m.JoinedAtUtc,
                    Wins = m.User.Wins,
                    Losses = m.User.Losses,
                    WinAssists = m.User.WinAssists,
                    TotalMoves = m.User.TotalMoves,
                    OptimalMoves = m.User.OptimalMoves,
                    AverageTurnSeconds = m.User.AverageTurnSeconds
                };
            })
            .ToList();

        // Only Captain and Vice-Captain can view and act on pending join requests
        bool canReviewRequests = membership.Role == TeamRole.Captain || membership.Role == TeamRole.ViceCaptain;
        List<TeamJoinRequestDto> requestDtos = canReviewRequests
            ? team.JoinRequests
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r =>
                {
                    int reqElo = r.User.Elo >= RoatanRankTierHelper.MinElo ? r.User.Elo : RoatanRankTierHelper.DefaultElo;
                    return new TeamJoinRequestDto
                    {
                        RequestId = r.Id,
                        TeamId = r.TeamId,
                        TeamName = team.Name,
                        UserId = r.UserId,
                        Username = r.User.Login,
                        AvatarBase64 = r.User.Avatar is not null && r.User.Avatar.Image is { Length: > 0 }
                            ? Convert.ToBase64String(r.User.Avatar.Image)
                            : null,
                        Elo = reqElo,
                        RankTier = RoatanRankTierHelper.FromElo(reqElo),
                        CreatedAtUtc = r.CreatedAtUtc
                    };
                })
                .ToList()
            : [];

        return new MyTeamDetailsDto
        {
            TeamInfo = teamInfo,
            MyRole = membership.Role,
            Members = memberDtos,
            PendingRequests = requestDtos,
            PendingInvites = [],
            StatusMessage = statusMessage
        };
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> CreateTeamAsync(long creatorUserId, CreateTeamDto request)
    {
        if (request is null) return TeamActionResult<MyTeamDetailsDto>.Fail("Invalid team payload.");

        bool alreadyInTeam = await dbContext.TeamMembers.AnyAsync(m => m.UserId == creatorUserId);
        if (alreadyInTeam)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are already in a team. Leave your current team before creating a new one.");
        }

        string? validationError = ValidateTeamNameAndTag(request.Name, request.Tag, out string cleanName, out string cleanTag);
        if (validationError is not null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail(validationError);
        }

        string lowerName = cleanName.ToLowerInvariant();
        if (await dbContext.Teams.AnyAsync(t => t.Name.ToLower() == lowerName))
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("A team with that name already exists. Please choose another name.");
        }

        string upperTag = cleanTag.ToUpperInvariant();
        if (await dbContext.Teams.AnyAsync(t => t.Tag.ToUpper() == upperTag))
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"The team tag [{upperTag}] is already taken.");
        }

        UserEntity? creator = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == creatorUserId);
        if (creator is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Account not found.");
        }

        DateTime utcNow = DateTime.UtcNow;
        TeamEntity team = new()
        {
            Name = cleanName,
            Tag = upperTag,
            TeamElo = RoatanRankTierHelper.DefaultElo,
            TeamWins = 0,
            TeamLosses = 0,
            MaxMembers = MaxTeamMembers,
            JoinPolicy = request.JoinPolicy,
            MinElo = Math.Clamp(request.MinElo, RoatanRankTierHelper.MinElo, 3000),
            ActivityRequirement = request.ActivityRequirement,
            CreatedAtUtc = utcNow
        };

        await dbContext.Teams.AddAsync(team);
        await dbContext.SaveChangesAsync();

        await dbContext.TeamMembers.AddAsync(new TeamMemberEntity
        {
            TeamId = team.Id,
            UserId = creatorUserId,
            Role = TeamRole.Captain,
            JoinedAtUtc = utcNow
        });

        await RemoveAllPendingRequestsAndInvitesForUserAsync(creatorUserId);
        await dbContext.SaveChangesAsync();

        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(creatorUserId, $"Team [{team.Tag}] {team.Name} created!"));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> UpdateTeamSettingsAsync(long actorUserId, UpdateTeamSettingsDto request)
    {
        if (request is null) return TeamActionResult<MyTeamDetailsDto>.Fail("Invalid settings payload.");

        TeamMemberEntity? actor = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == actorUserId);
        if (actor is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are not currently in a team.");
        }

        if (actor.Role != TeamRole.Captain)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Only the Team Captain can rename the team or edit team settings.");
        }

        TeamEntity? team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == actor.TeamId);
        if (team is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Team not found.");
        }

        string effectiveName = string.IsNullOrWhiteSpace(request.Name) ? team.Name : request.Name;
        string effectiveTag = string.IsNullOrWhiteSpace(request.Tag) ? team.Tag : request.Tag;
        string? validationError = ValidateTeamNameAndTag(effectiveName, effectiveTag, out string cleanName, out string cleanTag);
        if (validationError is not null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail(validationError);
        }

        string lowerName = cleanName.ToLowerInvariant();
        if (await dbContext.Teams.AnyAsync(t => t.Id != team.Id && t.Name.ToLower() == lowerName))
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Another team already uses that name.");
        }

        string upperTag = cleanTag.ToUpperInvariant();
        if (await dbContext.Teams.AnyAsync(t => t.Id != team.Id && t.Tag.ToUpper() == upperTag))
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"Another team already uses the tag [{upperTag}].");
        }

        team.Name = cleanName;
        team.Tag = upperTag;
        team.JoinPolicy = request.JoinPolicy;
        team.MinElo = Math.Clamp(request.MinElo, RoatanRankTierHelper.MinElo, 3000);
        team.ActivityRequirement = request.ActivityRequirement;

        await dbContext.SaveChangesAsync();
        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(actorUserId, "Team settings updated!"));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> JoinOrRequestTeamAsync(long userId, long teamId)
    {
        bool alreadyInTeam = await dbContext.TeamMembers.AnyAsync(m => m.UserId == userId);
        if (alreadyInTeam)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are already in a team. Leave your current team first.");
        }

        UserEntity? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Account not found.");
        }

        TeamEntity? team = await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == teamId);
        if (team is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Team not found.");
        }

        (bool eligible, string? disqualifyReason) = EvaluateEligibility(user, team, team.Members.Count, DateTime.UtcNow);
        if (!eligible)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail(disqualifyReason ?? "You do not meet this team's entry requirements.");
        }

        if (team.JoinPolicy == TeamJoinPolicy.Open)
        {
            await dbContext.TeamMembers.AddAsync(new TeamMemberEntity
            {
                TeamId = team.Id,
                UserId = userId,
                Role = TeamRole.Teammate,
                JoinedAtUtc = DateTime.UtcNow
            });
            await RemoveAllPendingRequestsAndInvitesForUserAsync(userId);
            await dbContext.SaveChangesAsync();

            return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(userId, $"Joined [{team.Tag}] {team.Name}!"));
        }

        bool alreadyRequested = await dbContext.TeamJoinRequests.AnyAsync(r => r.TeamId == team.Id && r.UserId == userId);
        if (alreadyRequested)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You already have a pending join request for this team.");
        }

        await dbContext.TeamJoinRequests.AddAsync(new TeamJoinRequestEntity
        {
            TeamId = team.Id,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return TeamActionResult<MyTeamDetailsDto>.Success(
            await GetMyTeamAsync(userId, $"Join request sent to [{team.Tag}] {team.Name}! Waiting for Captain or Vice-Captain approval."));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> RespondTeamJoinRequestAsync(long actorUserId, long requestId, bool approve)
    {
        TeamMemberEntity? actor = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == actorUserId);
        if (actor is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are not currently in a team.");
        }

        if (actor.Role != TeamRole.Captain && actor.Role != TeamRole.ViceCaptain)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Only the Captain or Vice-Captain can accept or deny join requests.");
        }

        TeamJoinRequestEntity? joinReq = await dbContext.TeamJoinRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TeamId == actor.TeamId);

        if (joinReq is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Join request not found or already handled.");
        }

        if (!approve)
        {
            string deniedName = joinReq.User?.Login ?? "Player";
            dbContext.TeamJoinRequests.Remove(joinReq);
            await dbContext.SaveChangesAsync();
            return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(actorUserId, $"Declined join request from {deniedName}."));
        }

        TeamEntity? team = await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == actor.TeamId);
        if (team is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Team not found.");
        }

        int maxMembers = ResolveMaxMembers(team);
        if (team.Members.Count >= maxMembers)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"This team is full ({team.Members.Count}/{maxMembers} members).");
        }

        bool applicantAlreadyInTeam = await dbContext.TeamMembers.AnyAsync(m => m.UserId == joinReq.UserId);
        if (applicantAlreadyInTeam)
        {
            dbContext.TeamJoinRequests.Remove(joinReq);
            await dbContext.SaveChangesAsync();
            return TeamActionResult<MyTeamDetailsDto>.Fail("That player has already joined another team.");
        }

        string approvedName = joinReq.User?.Login ?? "Player";
        await dbContext.TeamMembers.AddAsync(new TeamMemberEntity
        {
            TeamId = team.Id,
            UserId = joinReq.UserId,
            Role = TeamRole.Teammate,
            JoinedAtUtc = DateTime.UtcNow
        });

        await RemoveAllPendingRequestsAndInvitesForUserAsync(joinReq.UserId);
        await dbContext.SaveChangesAsync();

        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(actorUserId, $"{approvedName} joined the team!"));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> InviteToTeamAsync(long actorUserId, InviteToTeamDto request)
    {
        if (request is null) return TeamActionResult<MyTeamDetailsDto>.Fail("Invalid invite request.");

        // Captain, Vice-Captain, AND Teammates are all allowed to invite outside players
        TeamMemberEntity? actor = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == actorUserId);
        if (actor is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You must be in a team to invite players.");
        }

        TeamEntity? team = await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == actor.TeamId);
        if (team is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Team not found.");
        }

        int maxMembers = ResolveMaxMembers(team);
        if (team.Members.Count >= maxMembers)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"This team is full ({team.Members.Count}/{maxMembers} members).");
        }

        UserEntity? target = null;
        if (request.TargetUserId > 0)
        {
            target = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId);
        }
        else if (!string.IsNullOrWhiteSpace(request.TargetUsername))
        {
            string lower = request.TargetUsername.Trim().ToLowerInvariant();
            target = await dbContext.Users.FirstOrDefaultAsync(u => u.Login.ToLower() == lower);
        }

        if (target is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Player not found. Check the username and try again.");
        }

        if (target.Id == actorUserId)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You cannot invite yourself.");
        }

        bool targetAlreadyInTeam = await dbContext.TeamMembers.AnyAsync(m => m.UserId == target.Id);
        if (targetAlreadyInTeam)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"{target.Login} is already in a team.");
        }

        bool duplicateInvite = await dbContext.TeamInvites.AnyAsync(i => i.TeamId == team.Id && i.InvitedUserId == target.Id);
        if (duplicateInvite)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"{target.Login} already has a pending invite from your team.");
        }

        await dbContext.TeamInvites.AddAsync(new TeamInviteEntity
        {
            TeamId = team.Id,
            InvitedUserId = target.Id,
            InvitedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(actorUserId, $"Invited {target.Login} to [{team.Tag}] {team.Name}!"));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> RespondTeamInviteAsync(long userId, long inviteId, bool accept)
    {
        TeamInviteEntity? invite = await dbContext.TeamInvites
            .Include(i => i.Team)
                .ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.InvitedUserId == userId);

        if (invite is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Team invite not found or expired.");
        }

        if (!accept)
        {
            string declinedTeam = invite.Team?.Name ?? "team";
            dbContext.TeamInvites.Remove(invite);
            await dbContext.SaveChangesAsync();
            return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(userId, $"Declined invite to {declinedTeam}."));
        }

        bool alreadyInTeam = await dbContext.TeamMembers.AnyAsync(m => m.UserId == userId);
        if (alreadyInTeam)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are already in a team. Leave your current team first.");
        }

        if (invite.Team is null)
        {
            dbContext.TeamInvites.Remove(invite);
            await dbContext.SaveChangesAsync();
            return TeamActionResult<MyTeamDetailsDto>.Fail("That team no longer exists.");
        }

        int inviteTeamMax = ResolveMaxMembers(invite.Team);
        if (invite.Team.Members.Count >= inviteTeamMax)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail($"This team is full ({invite.Team.Members.Count}/{inviteTeamMax} members).");
        }

        string joinedLabel = $"[{invite.Team.Tag}] {invite.Team.Name}";
        await dbContext.TeamMembers.AddAsync(new TeamMemberEntity
        {
            TeamId = invite.TeamId,
            UserId = userId,
            Role = TeamRole.Teammate,
            JoinedAtUtc = DateTime.UtcNow
        });

        await RemoveAllPendingRequestsAndInvitesForUserAsync(userId);
        await dbContext.SaveChangesAsync();

        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(userId, $"Joined {joinedLabel}!"));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> PromoteOrDemoteMemberAsync(long actorUserId, long targetUserId, TeamRole newRole)
    {
        TeamMemberEntity? actor = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == actorUserId);
        if (actor is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are not currently in a team.");
        }

        if (actor.Role != TeamRole.Captain)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Only the Team Captain can promote or demote members.");
        }

        if (targetUserId == actorUserId)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You cannot change your own Captain role here.");
        }

        if (newRole != TeamRole.ViceCaptain && newRole != TeamRole.Teammate)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Members can only be promoted to Vice-Captain or demoted to Teammate.");
        }

        TeamMemberEntity? target = await dbContext.TeamMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.TeamId == actor.TeamId && m.UserId == targetUserId);
        if (target is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Member not found in your team.");
        }

        if (target.Role == TeamRole.Captain)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Cannot demote the Team Captain.");
        }

        target.Role = newRole;
        await dbContext.SaveChangesAsync();

        string verb = newRole == TeamRole.ViceCaptain ? "Promoted" : "Demoted";
        return TeamActionResult<MyTeamDetailsDto>.Success(
            await GetMyTeamAsync(actorUserId, $"{verb} {target.User.Login} to {newRole.GetDisplayName()}."));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> KickTeamMemberAsync(long actorUserId, long targetUserId)
    {
        TeamMemberEntity? actor = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == actorUserId);
        if (actor is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are not currently in a team.");
        }

        if (actor.Role == TeamRole.Teammate)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Teammates cannot kick members from the team.");
        }

        if (targetUserId == actorUserId)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Use Leave Team to leave your own team.");
        }

        TeamMemberEntity? target = await dbContext.TeamMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.TeamId == actor.TeamId && m.UserId == targetUserId);
        if (target is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Member not found in your team.");
        }

        if (target.Role == TeamRole.Captain)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You cannot kick the Team Captain.");
        }

        if (actor.Role == TeamRole.ViceCaptain && target.Role == TeamRole.ViceCaptain)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("Vice-Captains can only kick Teammates, not other Vice-Captains.");
        }

        string kickedName = target.User?.Login ?? "Member";
        dbContext.TeamMembers.Remove(target);
        await dbContext.SaveChangesAsync();

        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(actorUserId, $"Removed {kickedName} from the team."));
    }

    public async Task<TeamActionResult<MyTeamDetailsDto>> LeaveTeamAsync(long userId)
    {
        TeamMemberEntity? membership = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (membership is null)
        {
            return TeamActionResult<MyTeamDetailsDto>.Fail("You are not currently in a team.");
        }

        long teamId = membership.TeamId;
        bool wasCaptain = membership.Role == TeamRole.Captain;

        dbContext.TeamMembers.Remove(membership);
        await dbContext.SaveChangesAsync();

        List<TeamMemberEntity> remaining = await dbContext.TeamMembers
            .Where(m => m.TeamId == teamId)
            .OrderByDescending(m => m.Role) // ViceCaptains before Teammates
            .ThenBy(m => m.JoinedAtUtc)     // Longest-serving first
            .ThenBy(m => m.UserId)
            .ToListAsync();

        if (remaining.Count == 0)
        {
            TeamEntity? emptyTeam = await dbContext.Teams
                .Include(t => t.JoinRequests)
                .Include(t => t.Invites)
                .FirstOrDefaultAsync(t => t.Id == teamId);
            if (emptyTeam is not null)
            {
                List<TeamChallengeEntity> challenges = await dbContext.TeamChallenges
                    .Where(c => c.ChallengerTeamId == teamId || c.TargetTeamId == teamId)
                    .ToListAsync();
                if (challenges.Count > 0) dbContext.TeamChallenges.RemoveRange(challenges);
                dbContext.TeamJoinRequests.RemoveRange(emptyTeam.JoinRequests);
                dbContext.TeamInvites.RemoveRange(emptyTeam.Invites);
                dbContext.Teams.Remove(emptyTeam);
                await dbContext.SaveChangesAsync();
            }

            return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(userId, "You left and disbanded the team."));
        }

        if (wasCaptain)
        {
            remaining[0].Role = TeamRole.Captain;
            await dbContext.SaveChangesAsync();
        }

        return TeamActionResult<MyTeamDetailsDto>.Success(await GetMyTeamAsync(userId, "You left the team."));
    }

    public async Task<long?> GetUserTeamIdAsync(long userId)
    {
        TeamMemberEntity? membership = await dbContext.TeamMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        return membership?.TeamId;
    }

    public async Task<TeamLeaderboardDto> GetTeamLeaderboardAsync(long? requestingUserId = null, int limit = 100)
    {
        int cappedLimit = Math.Clamp(limit, 1, 500);
        long? myTeamId = requestingUserId.HasValue && requestingUserId.Value > 0
            ? await GetUserTeamIdAsync(requestingUserId.Value)
            : null;

        List<TeamEntity> allTeams = await dbContext.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .ToListAsync();

        List<TeamEntity> ordered = allTeams
            .OrderByDescending(t => RoatanRankTierHelper.ClampElo(t.TeamElo))
            .ThenByDescending(CalculateAverageMemberElo)
            .ThenByDescending(t => t.TeamWins)
            .ThenByDescending(t => t.Members.Count)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<TeamLeaderboardEntryDto> topEntries = [];
        TeamLeaderboardEntryDto? myTeamEntry = null;

        for (int i = 0; i < ordered.Count; i++)
        {
            TeamEntity t = ordered[i];
            int rank = i + 1;
            int clampedElo = RoatanRankTierHelper.ClampElo(t.TeamElo);
            int avgMemberElo = CalculateAverageMemberElo(t);
            RoatanRankTierInfo tierInfo = RoatanRankTierHelper.GetInfoForElo(clampedElo);
            string captainName = t.Members
                .FirstOrDefault(m => m.Role == TeamRole.Captain)?.User?.Login
                ?? t.Members.FirstOrDefault()?.User?.Login
                ?? "Captain";

            TeamLeaderboardEntryDto entry = new()
            {
                Rank = rank,
                TeamId = t.Id,
                Name = t.Name,
                Tag = t.Tag,
                TeamElo = clampedElo,
                AverageMemberElo = avgMemberElo,
                TeamRankTier = tierInfo.Tier,
                TierName = tierInfo.DisplayName,
                TierColorHex = tierInfo.BadgeColorHex,
                TeamWins = t.TeamWins,
                TeamLosses = t.TeamLosses,
                CaptainUsername = captainName,
                MemberCount = t.Members.Count,
                MaxMembers = ResolveMaxMembers(t)
            };

            if (i < cappedLimit)
            {
                topEntries.Add(entry);
            }

            if (myTeamId.HasValue && t.Id == myTeamId.Value)
            {
                myTeamEntry = entry;
            }
        }

        return new TeamLeaderboardDto
        {
            Entries = topEntries,
            MyTeamEntry = myTeamEntry,
            TotalTeams = ordered.Count
        };
    }

    public async Task<TeamActionResult<TeamChallengeActionResponseDto>> SendTeamChallengeAsync(
        IClient challengerClient,
        SendTeamChallengeDto request)
    {
        if (challengerClient.User is null || request is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Invalid Team Challenge request.");
        }

        if (lobbyManager is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Team Challenge matchmaking is unavailable.");
        }

        long actorUserId = challengerClient.User.Id;
        TeamMemberEntity? actorMembership = await dbContext.TeamMembers
            .FirstOrDefaultAsync(m => m.UserId == actorUserId);

        if (actorMembership is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("You must be in a team to send a Team Challenge.");
        }

        if (actorMembership.Role != TeamRole.Captain && actorMembership.Role != TeamRole.ViceCaptain)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Only a Team Captain or Vice-Captain can send a Team Challenge.");
        }

        if (actorMembership.TeamId == request.TargetTeamId)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("You cannot challenge your own team.");
        }

        TeamEntity? challengerTeam = await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == actorMembership.TeamId);
        TeamEntity? targetTeam = await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == request.TargetTeamId);

        if (challengerTeam is null || targetTeam is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Target team not found.");
        }

        MatchFormat mode = request.GameMode == MatchFormat.TWO_BEST ? MatchFormat.TWO_BEST : MatchFormat.SIX_LOV;
        TableStyle variant = request.Variant == TableStyle.BLOCK ? TableStyle.BLOCK : TableStyle.DRAW;

        // If challenger is currently seated in another waiting table, remove cleanly first
        Lobby? existingLobby = lobbyManager.GetLobbyForClient(challengerClient);
        if (existingLobby is not null)
        {
            lobbyManager.RemovePlayerFromLobby(challengerClient, existingLobby.Id);
        }

        DateTime utcNow = DateTime.UtcNow;
        TeamChallengeEntity challenge = new()
        {
            ChallengerTeamId = challengerTeam.Id,
            TargetTeamId = targetTeam.Id,
            GameMode = mode,
            Variant = variant,
            RequireFullHumanSquad = request.RequireFullHumanSquad,
            Status = TeamChallengeStatus.Pending,
            TeamACheckedIn = true,
            CreatedUtc = utcNow
        };
        await dbContext.TeamChallenges.AddAsync(challenge);
        await dbContext.SaveChangesAsync();

        Lobby challengeTable = lobbyManager.CreateTeamChallengeLobby(
            challengerClient,
            challenge.Id,
            challengerTeam.Id,
            challengerTeam.Name,
            challengerTeam.Tag,
            targetTeam.Id,
            targetTeam.Name,
            targetTeam.Tag,
            mode,
            variant,
            requireFullHumanSquad: request.RequireFullHumanSquad);

        challenge.TableId = challengeTable.Id;
        challenge.TableCode = challengeTable.Code;
        await dbContext.SaveChangesAsync();

        string modeText = mode == MatchFormat.TWO_BEST ? "Two-Best 1v1" : "Six-Lov 2v2";
        string variantText = variant == TableStyle.BLOCK ? "Block" : "Draw";
        TeamChallengeDto challengeDto = BuildChallengeDto(
            challenge,
            challengerTeam,
            targetTeam,
            $"[{challengerTeam.Tag}] {challengerTeam.Name} challenged your crew to {modeText} ({variantText})!");

        // Broadcast TeamChallengeNotification to all online Captains & Vice-Captains of the challenged team
        HashSet<long> targetLeaderIds = targetTeam.Members
            .Where(m => m.Role == TeamRole.Captain || m.Role == TeamRole.ViceCaptain)
            .Select(m => m.UserId)
            .ToHashSet();

        NetworkFrame notifFrame = new(MessageFrameBuilder.TeamChallengeNotificationFrame(challengeDto));
        foreach (IClient online in clientManager.GetAllLoggedInClients())
        {
            if (online.User is not null && targetLeaderIds.Contains(online.User.Id))
            {
                try { await online.SendAsync(notifFrame); } catch { }
            }
        }

        return TeamActionResult<TeamChallengeActionResponseDto>.Success(new TeamChallengeActionResponseDto
        {
            Challenge = challengeDto,
            Table = DtoBuilder.GetLobbyDtoObject(challengeTable)
        });
    }

    public async Task<TeamActionResult<TeamChallengeActionResponseDto>> RespondTeamChallengeAsync(
        IClient responderClient,
        RespondTeamChallengeDto request)
    {
        if (responderClient.User is null || request is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Invalid challenge response.");
        }

        if (lobbyManager is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Team Challenge matchmaking is unavailable.");
        }

        TeamChallengeEntity? challenge = await dbContext.TeamChallenges
            .Include(c => c.ChallengerTeam)
                .ThenInclude(t => t.Members)
            .Include(c => c.TargetTeam)
                .ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId);

        if (challenge is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Team Challenge not found.");
        }

        if (challenge.Status != TeamChallengeStatus.Pending)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail($"This challenge has already been {challenge.Status.ToString().ToLowerInvariant()}.");
        }

        if (challenge.IsOpenBoardChallenge && !challenge.TargetTeamId.HasValue)
        {
            if (!request.Accept)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Only the posting team can cancel an open board challenge.");
            }
            return await AcceptOpenTeamChallengeAsync(responderClient, request.ChallengeId);
        }

        long actorUserId = responderClient.User.Id;
        TeamMemberEntity? targetMembership = challenge.TargetTeam?.Members
            .FirstOrDefault(m => m.UserId == actorUserId);

        if (targetMembership is null ||
            (targetMembership.Role != TeamRole.Captain && targetMembership.Role != TeamRole.ViceCaptain))
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Only a Captain or Vice-Captain of the challenged team can accept or decline.");
        }

        if (!request.Accept)
        {
            challenge.Status = TeamChallengeStatus.Declined;
            await dbContext.SaveChangesAsync();

            if (challenge.TableId > 0 && lobbyManager.ExistById(challenge.TableId))
            {
                lobbyManager.DeleteLobby(challenge.TableId);
            }

            TeamChallengeDto declinedDto = BuildChallengeDto(
                challenge,
                challenge.ChallengerTeam,
                challenge.TargetTeam,
                $"[{challenge.TargetTeam!.Tag}] {challenge.TargetTeam.Name} declined the Team Challenge.");

            HashSet<long> challengerMemberIds = challenge.ChallengerTeam.Members.Select(m => m.UserId).ToHashSet();
            NetworkFrame declinedFrame = new(MessageFrameBuilder.TeamChallengeNotificationFrame(declinedDto));
            foreach (IClient online in clientManager.GetAllLoggedInClients())
            {
                if (online.User is not null && challengerMemberIds.Contains(online.User.Id))
                {
                    try { await online.SendAsync(declinedFrame); } catch { }
                }
            }

            return TeamActionResult<TeamChallengeActionResponseDto>.Success(new TeamChallengeActionResponseDto
            {
                Challenge = declinedDto,
                Table = null
            });
        }

        Lobby? challengeTable = lobbyManager.GetLobbyById(challenge.TableId);
        if (challengeTable is null)
        {
            challenge.Status = TeamChallengeStatus.Declined;
            await dbContext.SaveChangesAsync();
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("The Team Challenge table is no longer open.");
        }

        Lobby? responderCurrentLobby = lobbyManager.GetLobbyForClient(responderClient);
        if (responderCurrentLobby is not null && responderCurrentLobby.Id != challengeTable.Id)
        {
            lobbyManager.RemovePlayerFromLobby(responderClient, responderCurrentLobby.Id);
        }

        // Auto-seat the accepting leader into Team B's seat (Seat 1) & start the 24-hour play window
        lobbyManager.AddPlayerToTeamChallengeLobby(responderClient, challengeTable.Id, challenge.TargetTeamId);

        DateTime deadlineUtc = DateTime.UtcNow.AddHours(24);
        challenge.Status = TeamChallengeStatus.Accepted;
        challenge.PlayDeadlineUtc = deadlineUtc;
        challenge.TeamBCheckedIn = true;
        challengeTable.PlayDeadlineUtc = deadlineUtc;
        await dbContext.SaveChangesAsync();

        var updatedTableDto = DtoBuilder.GetLobbyDtoObject(challengeTable);
        TeamChallengeDto acceptedDto = BuildChallengeDto(
            challenge,
            challenge.ChallengerTeam,
            challenge.TargetTeam,
            $"Team Challenge ACCEPTED! [{challenge.ChallengerTeam.Tag}] vs [{challenge.TargetTeam!.Tag}] — 24h play window started (Code {challengeTable.Code})!");

        // Broadcast updated table state to seated players & notify both teams' online members so teammates can fill open seats
        HashSet<long> seatedIds = challengeTable.PlayersInLobby.Where(p => !p.IsBot).Select(p => p.Id).ToHashSet();
        HashSet<long> bothTeamMemberIds = challenge.ChallengerTeam.Members.Select(m => m.UserId)
            .Concat(challenge.TargetTeam.Members.Select(m => m.UserId))
            .ToHashSet();

        NetworkFrame tableUpdateFrame = new(MessageFrameBuilder.TableStateUpdatedFrame(updatedTableDto));
        NetworkFrame acceptedNotifFrame = new(MessageFrameBuilder.TeamChallengeNotificationFrame(acceptedDto));

        foreach (IClient online in clientManager.GetAllLoggedInClients())
        {
            if (online.User is null || online.User.Id == actorUserId) continue;

            if (seatedIds.Contains(online.User.Id))
            {
                try { await online.SendAsync(tableUpdateFrame); } catch { }
            }

            if (bothTeamMemberIds.Contains(online.User.Id))
            {
                try { await online.SendAsync(acceptedNotifFrame); } catch { }
            }
        }

        return TeamActionResult<TeamChallengeActionResponseDto>.Success(new TeamChallengeActionResponseDto
        {
            Challenge = acceptedDto,
            Table = updatedTableDto
        });
    }

    // ==================== OPEN TEAM CHALLENGE BOARD, ASK LEADERS & 24H WINDOW ====================

    public async Task<OpenTeamChallengesDto> GetOpenTeamChallengesAsync(long requestingUserId, DateTime? nowUtc = null)
    {
        DateTime utcNow = nowUtc ?? DateTime.UtcNow;

        TeamMemberEntity? myMembership = await dbContext.TeamMembers
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m => m.UserId == requestingUserId);
        TeamEntity? myTeam = myMembership?.Team;

        List<TeamChallengeEntity> openEntities = await dbContext.TeamChallenges
            .Include(c => c.ChallengerTeam)
                .ThenInclude(t => t.Members)
            .Include(c => c.Endorsements)
            .Where(c => c.IsOpenBoardChallenge &&
                        c.Status == TeamChallengeStatus.Pending &&
                        c.TargetTeamId == null &&
                        (!c.ExpiresUtc.HasValue || c.ExpiresUtc.Value > utcNow))
            .OrderByDescending(c => c.CreatedUtc)
            .ToListAsync();

        List<TeamChallengeDto> openDtos = openEntities
            .Select(c => BuildChallengeDto(c, c.ChallengerTeam, null, null, myTeam, requestingUserId))
            .ToList();

        List<TeamChallengeDto> activeAccepted = [];
        if (myTeam is not null)
        {
            List<TeamChallengeEntity> acceptedEntities = await dbContext.TeamChallenges
                .Include(c => c.ChallengerTeam)
                .Include(c => c.TargetTeam)
                .Where(c => c.Status == TeamChallengeStatus.Accepted &&
                            (c.ChallengerTeamId == myTeam.Id || c.TargetTeamId == myTeam.Id))
                .OrderBy(c => c.PlayDeadlineUtc ?? c.CreatedUtc)
                .ToListAsync();

            activeAccepted = acceptedEntities
                .Select(c => BuildChallengeDto(c, c.ChallengerTeam, c.TargetTeam, null, myTeam, requestingUserId))
                .ToList();
        }

        return new OpenTeamChallengesDto
        {
            OpenChallenges = openDtos,
            ActiveAcceptedChallenges = activeAccepted
        };
    }

    public async Task<TeamActionResult<TeamChallengeDto>> PostOpenTeamChallengeAsync(
        IClient posterClient,
        PostOpenTeamChallengeDto request,
        DateTime? nowUtc = null)
    {
        if (posterClient.User is null || request is null)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("Invalid open challenge request.");
        }

        long actorUserId = posterClient.User.Id;
        TeamMemberEntity? actorMembership = await dbContext.TeamMembers
            .Include(m => m.Team)
                .ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(m => m.UserId == actorUserId);

        if (actorMembership?.Team is null)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("You must be in a team to post an open challenge.");
        }

        if (actorMembership.Role != TeamRole.Captain && actorMembership.Role != TeamRole.ViceCaptain)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("Only a Team Captain or Vice-Captain can post an Open Board Challenge.");
        }

        DateTime utcNow = nowUtc ?? DateTime.UtcNow;
        MatchFormat mode = request.GameMode == MatchFormat.TWO_BEST ? MatchFormat.TWO_BEST : MatchFormat.SIX_LOV;
        TableStyle variant = request.Variant == TableStyle.BLOCK ? TableStyle.BLOCK : TableStyle.DRAW;
        int minRivalElo = RoatanRankTierHelper.ClampElo(request.MinOpponentTeamElo);

        TeamChallengeEntity challenge = new()
        {
            ChallengerTeamId = actorMembership.Team.Id,
            TargetTeamId = null,
            GameMode = mode,
            Variant = variant,
            IsOpenBoardChallenge = true,
            MinOpponentTeamElo = minRivalElo,
            RequireFullHumanSquad = request.RequireFullHumanSquad,
            Status = TeamChallengeStatus.Pending,
            CreatedUtc = utcNow,
            ExpiresUtc = utcNow.AddHours(48),
            TeamACheckedIn = false,
            TeamBCheckedIn = false
        };

        await dbContext.TeamChallenges.AddAsync(challenge);
        await dbContext.SaveChangesAsync();

        TeamChallengeDto dto = BuildChallengeDto(
            challenge,
            actorMembership.Team,
            null,
            $"Posted open challenge on the Challenge Board!",
            actorMembership.Team,
            actorUserId);

        return TeamActionResult<TeamChallengeDto>.Success(dto);
    }

    public async Task<TeamActionResult<TeamChallengeDto>> CancelOpenTeamChallengeAsync(long actorUserId, long challengeId)
    {
        TeamMemberEntity? actorMembership = await dbContext.TeamMembers
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m => m.UserId == actorUserId);

        if (actorMembership?.Team is null)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("You must be in a team to cancel a challenge.");
        }

        if (actorMembership.Role != TeamRole.Captain && actorMembership.Role != TeamRole.ViceCaptain)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("Only a Team Captain or Vice-Captain can cancel an open challenge.");
        }

        TeamChallengeEntity? challenge = await dbContext.TeamChallenges
            .Include(c => c.ChallengerTeam)
            .FirstOrDefaultAsync(c => c.Id == challengeId);

        if (challenge is null || !challenge.IsOpenBoardChallenge)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("Open challenge not found.");
        }

        if (challenge.ChallengerTeamId != actorMembership.TeamId)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("Only the posting team can cancel this open challenge.");
        }

        if (challenge.Status != TeamChallengeStatus.Pending)
        {
            return TeamActionResult<TeamChallengeDto>.Fail($"This challenge is already {challenge.Status.ToString().ToLowerInvariant()}.");
        }

        challenge.Status = TeamChallengeStatus.Cancelled;
        await dbContext.SaveChangesAsync();

        return TeamActionResult<TeamChallengeDto>.Success(BuildChallengeDto(
            challenge,
            challenge.ChallengerTeam,
            null,
            "Open challenge cancelled.",
            actorMembership.Team,
            actorUserId));
    }

    public async Task<TeamActionResult<TeamChallengeDto>> RequestAcceptTeamChallengeAsync(long actorUserId, long challengeId)
    {
        TeamMemberEntity? actorMembership = await dbContext.TeamMembers
            .Include(m => m.User)
            .Include(m => m.Team)
                .ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(m => m.UserId == actorUserId);

        if (actorMembership?.Team is null)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("You must be in a team to ask your leaders to accept a challenge.");
        }

        TeamChallengeEntity? challenge = await dbContext.TeamChallenges
            .Include(c => c.ChallengerTeam)
            .Include(c => c.Endorsements)
            .FirstOrDefaultAsync(c => c.Id == challengeId);

        if (challenge is null || !challenge.IsOpenBoardChallenge || challenge.Status != TeamChallengeStatus.Pending || challenge.TargetTeamId.HasValue)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("That open challenge is no longer available.");
        }

        if (challenge.ChallengerTeamId == actorMembership.TeamId)
        {
            return TeamActionResult<TeamChallengeDto>.Fail("You cannot request acceptance of your own team's challenge.");
        }

        (bool eligible, string? reason) = EvaluateChallengeConditions(challenge, actorMembership.Team);
        if (!eligible)
        {
            return TeamActionResult<TeamChallengeDto>.Fail(reason ?? "Your team does not meet this challenge's requirements.");
        }

        string actorUsername = actorMembership.User?.Login ?? $"Player {actorUserId}";
        bool alreadyEndorsed = challenge.Endorsements.Any(e => e.UserId == actorUserId);
        if (!alreadyEndorsed)
        {
            TeamChallengeEndorsementEntity endorsement = new()
            {
                ChallengeId = challenge.Id,
                TeamId = actorMembership.TeamId,
                UserId = actorUserId,
                Username = actorUsername,
                CreatedUtc = DateTime.UtcNow
            };
            await dbContext.TeamChallengeEndorsements.AddAsync(endorsement);
            challenge.Endorsements.Add(endorsement);
            await dbContext.SaveChangesAsync();
        }

        string leaderMessage = $"{actorUsername} asked leaders to accept [{challenge.ChallengerTeam.Tag}] {challenge.ChallengerTeam.Name}'s challenge!";
        TeamChallengeDto notifDto = BuildChallengeDto(
            challenge,
            challenge.ChallengerTeam,
            null,
            leaderMessage,
            actorMembership.Team,
            actorUserId);

        HashSet<long> leaderUserIds = actorMembership.Team.Members
            .Where(m => m.Role == TeamRole.Captain || m.Role == TeamRole.ViceCaptain)
            .Select(m => m.UserId)
            .ToHashSet();

        NetworkFrame notifFrame = new(MessageFrameBuilder.TeamChallengeNotificationFrame(notifDto));
        foreach (IClient online in clientManager.GetAllLoggedInClients())
        {
            if (online.User is not null && leaderUserIds.Contains(online.User.Id))
            {
                try { await online.SendAsync(notifFrame); } catch { }
            }
        }

        return TeamActionResult<TeamChallengeDto>.Success(notifDto);
    }

    public async Task<TeamActionResult<TeamChallengeActionResponseDto>> AcceptOpenTeamChallengeAsync(
        IClient responderClient,
        long challengeId,
        DateTime? nowUtc = null)
    {
        if (responderClient.User is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("You must be signed in to accept an open challenge.");
        }

        if (lobbyManager is null)
        {
            return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Team Challenge matchmaking is unavailable.");
        }

        await OpenChallengeClaimLock.WaitAsync();
        try
        {
            long actorUserId = responderClient.User.Id;
            TeamMemberEntity? actorMembership = await dbContext.TeamMembers
                .Include(m => m.Team)
                    .ThenInclude(t => t.Members)
                .FirstOrDefaultAsync(m => m.UserId == actorUserId);

            if (actorMembership?.Team is null)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("You must be in a team to accept a Team Challenge.");
            }

            if (actorMembership.Role != TeamRole.Captain && actorMembership.Role != TeamRole.ViceCaptain)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Only a Team Captain or Vice-Captain can accept an Open Board Challenge.");
            }

            TeamChallengeEntity? challenge = await dbContext.TeamChallenges
                .Include(c => c.ChallengerTeam)
                    .ThenInclude(t => t.Members)
                .Include(c => c.Endorsements)
                .FirstOrDefaultAsync(c => c.Id == challengeId);

            if (challenge is null || !challenge.IsOpenBoardChallenge)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("Open challenge not found.");
            }

            DateTime utcNow = nowUtc ?? DateTime.UtcNow;
            if (challenge.Status != TeamChallengeStatus.Pending || challenge.TargetTeamId.HasValue)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("This open challenge has already been claimed by another crew.");
            }

            if (challenge.ExpiresUtc.HasValue && challenge.ExpiresUtc.Value <= utcNow)
            {
                challenge.Status = TeamChallengeStatus.Cancelled;
                await dbContext.SaveChangesAsync();
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("This open challenge has expired.");
            }

            TeamEntity responderTeam = actorMembership.Team;
            if (challenge.ChallengerTeamId == responderTeam.Id)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail("You cannot accept your own team's open challenge.");
            }

            (bool eligible, string? disqualifyReason) = EvaluateChallengeConditions(challenge, responderTeam);
            if (!eligible)
            {
                return TeamActionResult<TeamChallengeActionResponseDto>.Fail(disqualifyReason ?? "Your team does not meet this challenge's requirements.");
            }

            Lobby? responderCurrentLobby = lobbyManager.GetLobbyForClient(responderClient);
            if (responderCurrentLobby is not null)
            {
                lobbyManager.RemovePlayerFromLobby(responderClient, responderCurrentLobby.Id);
            }

            DateTime deadlineUtc = utcNow.AddHours(24);
            Lobby challengeTable = lobbyManager.CreateTeamChallengeLobby(
                responderClient,
                challenge.Id,
                challenge.ChallengerTeam.Id,
                challenge.ChallengerTeam.Name,
                challenge.ChallengerTeam.Tag,
                responderTeam.Id,
                responderTeam.Name,
                responderTeam.Tag,
                challenge.GameMode,
                challenge.Variant,
                requireFullHumanSquad: challenge.RequireFullHumanSquad,
                playDeadlineUtc: deadlineUtc,
                seatCreatorOnTeamB: true);

            challenge.TargetTeamId = responderTeam.Id;
            challenge.TargetTeam = responderTeam;
            challenge.TableId = challengeTable.Id;
            challenge.TableCode = challengeTable.Code;
            challenge.Status = TeamChallengeStatus.Accepted;
            challenge.PlayDeadlineUtc = deadlineUtc;
            challenge.TeamBCheckedIn = true;
            await dbContext.SaveChangesAsync();

            var tableDto = DtoBuilder.GetLobbyDtoObject(challengeTable);
            TeamChallengeDto acceptedDto = BuildChallengeDto(
                challenge,
                challenge.ChallengerTeam,
                responderTeam,
                $"Open Challenge ACCEPTED! [{challenge.ChallengerTeam.Tag}] vs [{responderTeam.Tag}] — 24h window to play (Code {challengeTable.Code})!");

            HashSet<long> bothTeamMemberIds = challenge.ChallengerTeam.Members.Select(m => m.UserId)
                .Concat(responderTeam.Members.Select(m => m.UserId))
                .ToHashSet();

            NetworkFrame acceptedNotifFrame = new(MessageFrameBuilder.TeamChallengeNotificationFrame(acceptedDto));
            foreach (IClient online in clientManager.GetAllLoggedInClients())
            {
                if (online.User is null || online.User.Id == actorUserId) continue;
                if (bothTeamMemberIds.Contains(online.User.Id))
                {
                    try { await online.SendAsync(acceptedNotifFrame); } catch { }
                }
            }

            return TeamActionResult<TeamChallengeActionResponseDto>.Success(new TeamChallengeActionResponseDto
            {
                Challenge = acceptedDto,
                Table = tableDto
            });
        }
        finally
        {
            OpenChallengeClaimLock.Release();
        }
    }

    public async Task MarkTeamCheckedInAsync(long? challengeId, long? playerTeamId)
    {
        if (!challengeId.HasValue || challengeId.Value <= 0 || !playerTeamId.HasValue) return;
        TeamChallengeEntity? challenge = await dbContext.TeamChallenges.FirstOrDefaultAsync(c => c.Id == challengeId.Value);
        if (challenge is null || challenge.Status != TeamChallengeStatus.Accepted) return;

        bool changed = false;
        if (challenge.ChallengerTeamId == playerTeamId.Value && !challenge.TeamACheckedIn)
        {
            challenge.TeamACheckedIn = true;
            changed = true;
        }
        else if (challenge.TargetTeamId.HasValue && challenge.TargetTeamId.Value == playerTeamId.Value && !challenge.TeamBCheckedIn)
        {
            challenge.TeamBCheckedIn = true;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Deterministic sweep resolving expired Open Board listings and accepted Team Challenges past their 24h play deadline.
    /// - Only Team A checked in: Team A +16 TeamElo (Wins+1), Team B -16 TeamElo (Losses+1, floor 100), status ForfeitWinA.
    /// - Only Team B checked in: Team B +16 TeamElo (Wins+1), Team A -16 TeamElo (Losses+1, floor 100), status ForfeitWinB.
    /// - Neither (or no game completed): Both teams -12 TeamElo (floor 100), status ExpiredMutualNoShow.
    /// </summary>
    public async Task<int> ResolveExpiredChallengesAsync(DateTime? nowUtc = null)
    {
        DateTime utcNow = nowUtc ?? DateTime.UtcNow;
        int resolvedCount = 0;

        List<TeamChallengeEntity> expiredOpen = await dbContext.TeamChallenges
            .Where(c => c.IsOpenBoardChallenge &&
                        c.Status == TeamChallengeStatus.Pending &&
                        c.ExpiresUtc.HasValue &&
                        c.ExpiresUtc.Value <= utcNow)
            .ToListAsync();

        foreach (TeamChallengeEntity open in expiredOpen)
        {
            open.Status = TeamChallengeStatus.Cancelled;
            resolvedCount++;
        }

        List<TeamChallengeEntity> expiredAccepted = await dbContext.TeamChallenges
            .Include(c => c.ChallengerTeam)
            .Include(c => c.TargetTeam)
            .Where(c => c.Status == TeamChallengeStatus.Accepted &&
                        c.PlayDeadlineUtc.HasValue &&
                        c.PlayDeadlineUtc.Value <= utcNow)
            .ToListAsync();

        foreach (TeamChallengeEntity ch in expiredAccepted)
        {
            TeamEntity? teamA = ch.ChallengerTeam;
            TeamEntity? teamB = ch.TargetTeam;

            if (teamA is not null && teamB is not null)
            {
                if (ch.TeamACheckedIn && !ch.TeamBCheckedIn)
                {
                    teamA.TeamElo = RoatanRankTierHelper.ClampElo(teamA.TeamElo + NoShowForfeitEloDelta);
                    teamA.TeamWins++;
                    teamB.TeamElo = RoatanRankTierHelper.ClampElo(teamB.TeamElo - NoShowForfeitEloDelta);
                    teamB.TeamLosses++;
                    ch.Status = TeamChallengeStatus.ForfeitWinA;
                }
                else if (!ch.TeamACheckedIn && ch.TeamBCheckedIn)
                {
                    teamB.TeamElo = RoatanRankTierHelper.ClampElo(teamB.TeamElo + NoShowForfeitEloDelta);
                    teamB.TeamWins++;
                    teamA.TeamElo = RoatanRankTierHelper.ClampElo(teamA.TeamElo - NoShowForfeitEloDelta);
                    teamA.TeamLosses++;
                    ch.Status = TeamChallengeStatus.ForfeitWinB;
                }
                else
                {
                    teamA.TeamElo = RoatanRankTierHelper.ClampElo(teamA.TeamElo - MutualNoShowEloPenalty);
                    teamB.TeamElo = RoatanRankTierHelper.ClampElo(teamB.TeamElo - MutualNoShowEloPenalty);
                    ch.Status = TeamChallengeStatus.ExpiredMutualNoShow;
                }
            }
            else
            {
                ch.Status = TeamChallengeStatus.ExpiredMutualNoShow;
            }

            if (lobbyManager is not null && ch.TableId > 0 && lobbyManager.ExistById(ch.TableId))
            {
                lobbyManager.DeleteLobby(ch.TableId);
            }

            resolvedCount++;
        }

        if (resolvedCount > 0)
        {
            await dbContext.SaveChangesAsync();
        }

        return resolvedCount;
    }

    public async Task<TeamEloBreakdownDto?> ApplyTeamChallengeAbandonmentPenaltyAsync(
        long? challengeId,
        long teamAId,
        long teamBId,
        long abandoningTeamId)
    {
        if (challengeId.HasValue && challengeId.Value > 0)
        {
            TeamChallengeEntity? existing = await dbContext.TeamChallenges.FirstOrDefaultAsync(c => c.Id == challengeId.Value);
            if (existing is not null && existing.Status == TeamChallengeStatus.Completed)
            {
                return null;
            }
        }

        TeamEntity? teamA = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamAId);
        TeamEntity? teamB = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamBId);
        if (teamA is null || teamB is null) return null;

        bool teamAAbandoned = abandoningTeamId == teamAId;
        int prevA = RoatanRankTierHelper.ClampElo(teamA.TeamElo);
        int prevB = RoatanRankTierHelper.ClampElo(teamB.TeamElo);

        int newA = RoatanRankTierHelper.ClampElo(prevA + (teamAAbandoned ? -AbandonmentTeamEloDelta : AbandonmentTeamEloDelta));
        int newB = RoatanRankTierHelper.ClampElo(prevB + (teamAAbandoned ? AbandonmentTeamEloDelta : -AbandonmentTeamEloDelta));

        teamA.TeamElo = newA;
        teamB.TeamElo = newB;

        if (teamAAbandoned)
        {
            teamA.TeamLosses++;
            teamB.TeamWins++;
        }
        else
        {
            teamB.TeamLosses++;
            teamA.TeamWins++;
        }

        if (challengeId.HasValue && challengeId.Value > 0)
        {
            TeamChallengeEntity? challenge = await dbContext.TeamChallenges.FirstOrDefaultAsync(c => c.Id == challengeId.Value);
            if (challenge is not null)
            {
                challenge.Status = TeamChallengeStatus.Completed;
            }
        }

        await dbContext.SaveChangesAsync();

        RoatanRankTierInfo infoA = RoatanRankTierHelper.GetInfoForElo(newA);
        RoatanRankTierInfo infoB = RoatanRankTierHelper.GetInfoForElo(newB);

        return new TeamEloBreakdownDto
        {
            ChallengeId = challengeId ?? 0,
            TeamA = new TeamEloChangeDto
            {
                TeamId = teamA.Id,
                TeamName = teamA.Name,
                TeamTag = teamA.Tag,
                PreviousElo = prevA,
                NewElo = newA,
                Delta = newA - prevA,
                PreviousTier = RoatanRankTierHelper.FromElo(prevA),
                NewTier = infoA.Tier,
                TierName = infoA.DisplayName,
                TierColorHex = infoA.BadgeColorHex,
                ResultLabel = teamAAbandoned ? "Abandoned (-15)" : "Forfeit Win (+15)"
            },
            TeamB = new TeamEloChangeDto
            {
                TeamId = teamB.Id,
                TeamName = teamB.Name,
                TeamTag = teamB.Tag,
                PreviousElo = prevB,
                NewElo = newB,
                Delta = newB - prevB,
                PreviousTier = RoatanRankTierHelper.FromElo(prevB),
                NewTier = infoB.Tier,
                TierName = infoB.DisplayName,
                TierColorHex = infoB.BadgeColorHex,
                ResultLabel = teamAAbandoned ? "Forfeit Win (+15)" : "Abandoned (-15)"
            }
        };
    }

    public static (bool MeetsConditions, string? DisqualifyReason) EvaluateChallengeConditions(
        TeamChallengeEntity challenge,
        TeamEntity? viewerTeam)
    {
        if (viewerTeam is null)
        {
            return (false, "Join a team to accept Team Challenges.");
        }

        int minElo = RoatanRankTierHelper.ClampElo(challenge.MinOpponentTeamElo);
        int teamElo = RoatanRankTierHelper.ClampElo(viewerTeam.TeamElo);
        if (teamElo < minElo)
        {
            string tierName = RoatanRankTierHelper.FromElo(minElo).GetDisplayName();
            return (false, $"Requires {tierName} Team Elo ({minElo}+ Elo).");
        }

        return (true, null);
    }

    public async Task<TeamEloBreakdownDto?> ApplyTeamChallengeMatchResultAsync(
        long? challengeId,
        long teamAId,
        long teamBId,
        int winningTeamSlot,
        bool isDraw,
        bool isKeyWin,
        bool isGameBrukWin)
    {
        TeamEntity? teamA = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamAId);
        TeamEntity? teamB = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamBId);
        if (teamA is null || teamB is null) return null;

        TeamEloBreakdownDto breakdown = RoatanEloCalculator.CalculateTeamChallengeElo(new TeamChallengeEloInput
        {
            ChallengeId = challengeId ?? 0,
            TeamAId = teamA.Id,
            TeamAName = teamA.Name,
            TeamATag = teamA.Tag,
            TeamACurrentElo = teamA.TeamElo,
            TeamBId = teamB.Id,
            TeamBName = teamB.Name,
            TeamBTag = teamB.Tag,
            TeamBCurrentElo = teamB.TeamElo,
            WinningTeamSlot = winningTeamSlot,
            IsDraw = isDraw,
            IsKeyWin = isKeyWin,
            IsGameBrukWin = isGameBrukWin
        });

        teamA.TeamElo = breakdown.TeamA.NewElo;
        teamB.TeamElo = breakdown.TeamB.NewElo;

        if (!isDraw && winningTeamSlot == 0)
        {
            teamA.TeamWins++;
            teamB.TeamLosses++;
        }
        else if (!isDraw && winningTeamSlot == 1)
        {
            teamB.TeamWins++;
            teamA.TeamLosses++;
        }

        if (challengeId.HasValue && challengeId.Value > 0)
        {
            TeamChallengeEntity? challenge = await dbContext.TeamChallenges.FirstOrDefaultAsync(c => c.Id == challengeId.Value);
            if (challenge is not null)
            {
                challenge.Status = TeamChallengeStatus.Completed;
            }
        }

        await dbContext.SaveChangesAsync();
        return breakdown;
    }

    private static TeamChallengeDto BuildChallengeDto(
        TeamChallengeEntity challenge,
        TeamEntity challengerTeam,
        TeamEntity? targetTeam,
        string? message = null,
        TeamEntity? viewerTeam = null,
        long? viewerUserId = null)
    {
        bool meets = true;
        string? disqualifyReason = null;
        if (challenge.IsOpenBoardChallenge && !challenge.TargetTeamId.HasValue && viewerTeam is not null && viewerTeam.Id != challengerTeam.Id)
        {
            (meets, disqualifyReason) = EvaluateChallengeConditions(challenge, viewerTeam);
        }

        List<string> teamEndorsements = [];
        bool alreadyRequestedByMe = false;
        if (challenge.Endorsements is not null && viewerTeam is not null)
        {
            List<TeamChallengeEndorsementEntity> myTeamEndorsements = challenge.Endorsements
                .Where(e => e.TeamId == viewerTeam.Id)
                .OrderBy(e => e.CreatedUtc)
                .ToList();
            teamEndorsements = myTeamEndorsements
                .Select(e => e.Username)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (viewerUserId.HasValue)
            {
                alreadyRequestedByMe = myTeamEndorsements.Any(e => e.UserId == viewerUserId.Value);
            }
        }

        return new TeamChallengeDto
        {
            ChallengeId = challenge.Id,
            ChallengerTeamId = challengerTeam.Id,
            ChallengerTeamName = challengerTeam.Name,
            ChallengerTeamTag = challengerTeam.Tag,
            ChallengerTeamElo = RoatanRankTierHelper.ClampElo(challengerTeam.TeamElo),
            TargetTeamId = targetTeam?.Id ?? challenge.TargetTeamId,
            TargetTeamName = targetTeam?.Name,
            TargetTeamTag = targetTeam?.Tag,
            TargetTeamElo = targetTeam is not null ? RoatanRankTierHelper.ClampElo(targetTeam.TeamElo) : RoatanRankTierHelper.DefaultElo,
            GameMode = challenge.GameMode,
            Variant = challenge.Variant,
            TableId = challenge.TableId,
            TableCode = challenge.TableCode,
            Status = challenge.Status,
            CreatedUtc = challenge.CreatedUtc,
            Message = message,
            IsOpenBoardChallenge = challenge.IsOpenBoardChallenge,
            MinOpponentTeamElo = RoatanRankTierHelper.ClampElo(challenge.MinOpponentTeamElo),
            RequireFullHumanSquad = challenge.RequireFullHumanSquad,
            MeetsConditions = meets,
            DisqualifyReason = disqualifyReason,
            ExpiresUtc = challenge.ExpiresUtc,
            PlayDeadlineUtc = challenge.PlayDeadlineUtc,
            TeamACheckedIn = challenge.TeamACheckedIn,
            TeamBCheckedIn = challenge.TeamBCheckedIn,
            RequestedByTeammateUsernames = teamEndorsements,
            AlreadyRequestedByMe = alreadyRequestedByMe
        };
    }

    private async Task<List<TeamInviteDto>> GetPendingInvitesForUserAsync(long userId)
    {
        List<TeamInviteEntity> invites = await dbContext.TeamInvites
            .Include(i => i.Team)
            .Include(i => i.InvitedUser)
            .Include(i => i.InvitedByUser)
            .Where(i => i.InvitedUserId == userId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();

        return invites.Select(i =>
        {
            int teamElo = i.Team is not null ? RoatanRankTierHelper.ClampElo(i.Team.TeamElo) : RoatanRankTierHelper.DefaultElo;
            return new TeamInviteDto
            {
                InviteId = i.Id,
                TeamId = i.TeamId,
                TeamName = i.Team?.Name ?? "Team",
                TeamTag = i.Team?.Tag ?? "TEAM",
                TeamElo = teamElo,
                TeamRankTier = RoatanRankTierHelper.FromElo(teamElo),
                InvitedUserId = i.InvitedUserId,
                InvitedUsername = i.InvitedUser?.Login,
                InvitedByUserId = i.InvitedByUserId,
                InvitedByUsername = i.InvitedByUser?.Login ?? "Member",
                CreatedAtUtc = i.CreatedAtUtc
            };
        }).ToList();
    }

    private async Task RemoveAllPendingRequestsAndInvitesForUserAsync(long userId)
    {
        List<TeamJoinRequestEntity> requests = await dbContext.TeamJoinRequests
            .Where(r => r.UserId == userId)
            .ToListAsync();
        if (requests.Count > 0) dbContext.TeamJoinRequests.RemoveRange(requests);

        List<TeamInviteEntity> invites = await dbContext.TeamInvites
            .Where(i => i.InvitedUserId == userId)
            .ToListAsync();
        if (invites.Count > 0) dbContext.TeamInvites.RemoveRange(invites);
    }

    private static TeamInfoDto BuildTeamInfoDto(TeamEntity team, UserEntity? evaluatingUser, DateTime utcNow)
    {
        int clampedTeamElo = RoatanRankTierHelper.ClampElo(team.TeamElo);
        string captainUsername = team.Members
            .FirstOrDefault(m => m.Role == TeamRole.Captain)?.User?.Login
            ?? team.Members.FirstOrDefault()?.User?.Login
            ?? "Captain";

        bool meets = true;
        string? disqualifyReason = null;
        if (evaluatingUser is not null)
        {
            (meets, disqualifyReason) = EvaluateEligibility(evaluatingUser, team, team.Members.Count, utcNow);
        }

        return new TeamInfoDto
        {
            Id = team.Id,
            Name = team.Name,
            Tag = team.Tag,
            TeamElo = clampedTeamElo,
            AverageMemberElo = CalculateAverageMemberElo(team),
            TeamRankTier = RoatanRankTierHelper.FromElo(clampedTeamElo),
            TeamWins = team.TeamWins,
            TeamLosses = team.TeamLosses,
            CaptainUsername = captainUsername,
            MemberCount = team.Members.Count,
            MaxMembers = ResolveMaxMembers(team),
            JoinPolicy = team.JoinPolicy,
            MinElo = RoatanRankTierHelper.ClampElo(team.MinElo),
            ActivityRequirement = team.ActivityRequirement,
            MeetsRequirements = meets,
            DisqualifyReason = disqualifyReason
        };
    }

    private static string? ValidateTeamNameAndTag(string? rawName, string? rawTag, out string cleanName, out string cleanTag)
    {
        cleanName = rawName?.Trim() ?? string.Empty;
        cleanTag = rawTag?.Trim().ToUpperInvariant() ?? string.Empty;

        if (cleanName.Length < 3 || cleanName.Length > 24)
        {
            return "Team name must be between 3 and 24 characters.";
        }

        if (!TagPattern.IsMatch(cleanTag))
        {
            return "Team tag must be 3 to 4 letters or numbers (e.g. RTN, REEF).";
        }

        return null;
    }
}
