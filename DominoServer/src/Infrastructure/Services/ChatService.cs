using Application.Interfaces;
using Application.Interfaces.Managers;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ChatActionResult<T>
{
    public bool Ok { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Data { get; init; }

    public static ChatActionResult<T> Success(T data) => new() { Ok = true, Data = data };
    public static ChatActionResult<T> Fail(string message) => new() { Ok = false, ErrorMessage = message };
}

public class ChatService(
    ApplicationDbContext dbContext,
    IClientManager clientManager)
{
    public const int MaxMessageLength = 500;

    public async Task<ChatActionResult<ChatMessageDto>> SendMessageAsync(long senderUserId, ChatMessageDto request)
    {
        if (request is null) return ChatActionResult<ChatMessageDto>.Fail("Invalid chat message.");
        string body = (request.Body ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(body))
        {
            return ChatActionResult<ChatMessageDto>.Fail("Message cannot be empty.");
        }
        if (body.Length > MaxMessageLength)
        {
            body = body.Substring(0, MaxMessageLength);
        }

        UserEntity? sender = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == senderUserId);
        if (sender is null)
        {
            return ChatActionResult<ChatMessageDto>.Fail("Sender account not found.");
        }

        if (request.ChannelType == SocialChatChannelType.Team)
        {
            TeamMemberEntity? membership = await dbContext.TeamMembers
                .Include(m => m.Team)
                    .ThenInclude(t => t.Members)
                .FirstOrDefaultAsync(m => m.UserId == senderUserId);

            if (membership?.Team is null)
            {
                return ChatActionResult<ChatMessageDto>.Fail("You must be in a team to send Team Chat messages.");
            }

            if (request.TeamId.HasValue && request.TeamId.Value > 0 && request.TeamId.Value != membership.TeamId)
            {
                return ChatActionResult<ChatMessageDto>.Fail("You can only send Team Chat messages to your own team.");
            }

            SocialChatMessageEntity entity = new()
            {
                ChannelType = SocialChatChannelType.Team,
                TeamId = membership.TeamId,
                SenderUserId = sender.Id,
                RecipientUserId = null,
                Body = body,
                SentUtc = DateTime.UtcNow
            };
            await dbContext.SocialChatMessages.AddAsync(entity);
            await dbContext.SaveChangesAsync();

            ChatMessageDto dto = new()
            {
                MessageId = entity.Id,
                ChannelType = SocialChatChannelType.Team,
                TeamId = membership.TeamId,
                SenderUserId = sender.Id,
                SenderUsername = sender.Login,
                RecipientUserId = null,
                RecipientUsername = null,
                Body = entity.Body,
                SentUtc = entity.SentUtc
            };

            HashSet<long> memberIds = membership.Team.Members.Select(m => m.UserId).ToHashSet();
            await PushChatMessageAsync(dto, memberIds);
            return ChatActionResult<ChatMessageDto>.Success(dto);
        }
        else
        {
            long recipientId = request.RecipientUserId ?? 0;
            if (recipientId <= 0)
            {
                return ChatActionResult<ChatMessageDto>.Fail("Please select a recipient for your message.");
            }
            if (recipientId == senderUserId)
            {
                return ChatActionResult<ChatMessageDto>.Fail("You cannot send a direct message to yourself.");
            }

            UserEntity? recipient = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == recipientId);
            if (recipient is null)
            {
                return ChatActionResult<ChatMessageDto>.Fail("Recipient player not found.");
            }

            SocialChatMessageEntity entity = new()
            {
                ChannelType = SocialChatChannelType.Direct,
                TeamId = null,
                SenderUserId = sender.Id,
                RecipientUserId = recipient.Id,
                Body = body,
                SentUtc = DateTime.UtcNow
            };
            await dbContext.SocialChatMessages.AddAsync(entity);
            await dbContext.SaveChangesAsync();

            ChatMessageDto dto = new()
            {
                MessageId = entity.Id,
                ChannelType = SocialChatChannelType.Direct,
                TeamId = null,
                SenderUserId = sender.Id,
                SenderUsername = sender.Login,
                RecipientUserId = recipient.Id,
                RecipientUsername = recipient.Login,
                Body = entity.Body,
                SentUtc = entity.SentUtc
            };

            HashSet<long> participants = [sender.Id, recipient.Id];
            await PushChatMessageAsync(dto, participants);
            return ChatActionResult<ChatMessageDto>.Success(dto);
        }
    }

    public async Task<ChatActionResult<ChatHistoryDto>> GetHistoryAsync(long requestingUserId, GetChatHistoryDto request)
    {
        request ??= new GetChatHistoryDto();
        int limit = Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, 200);

        if (request.ChannelType == SocialChatChannelType.Team)
        {
            TeamMemberEntity? membership = await dbContext.TeamMembers
                .FirstOrDefaultAsync(m => m.UserId == requestingUserId);
            if (membership is null)
            {
                return ChatActionResult<ChatHistoryDto>.Success(new ChatHistoryDto
                {
                    ChannelType = SocialChatChannelType.Team,
                    TeamId = null,
                    Messages = []
                });
            }

            long teamId = membership.TeamId;
            List<SocialChatMessageEntity> rows = await dbContext.SocialChatMessages
                .Include(m => m.SenderUser)
                .Where(m => m.ChannelType == SocialChatChannelType.Team && m.TeamId == teamId)
                .OrderByDescending(m => m.SentUtc)
                .ThenByDescending(m => m.Id)
                .Take(limit)
                .ToListAsync();

            rows.Reverse();
            return ChatActionResult<ChatHistoryDto>.Success(new ChatHistoryDto
            {
                ChannelType = SocialChatChannelType.Team,
                TeamId = teamId,
                Messages = rows.Select(MapEntity).ToList()
            });
        }
        else
        {
            IQueryable<SocialChatMessageEntity> query = dbContext.SocialChatMessages
                .Include(m => m.SenderUser)
                .Include(m => m.RecipientUser)
                .Where(m => m.ChannelType == SocialChatChannelType.Direct);

            if (request.OtherUserId.HasValue && request.OtherUserId.Value > 0)
            {
                long otherId = request.OtherUserId.Value;
                query = query.Where(m =>
                    (m.SenderUserId == requestingUserId && m.RecipientUserId == otherId) ||
                    (m.SenderUserId == otherId && m.RecipientUserId == requestingUserId));
            }
            else
            {
                query = query.Where(m =>
                    m.SenderUserId == requestingUserId || m.RecipientUserId == requestingUserId);
            }

            List<SocialChatMessageEntity> rows = await query
                .OrderByDescending(m => m.SentUtc)
                .ThenByDescending(m => m.Id)
                .Take(limit)
                .ToListAsync();

            rows.Reverse();
            return ChatActionResult<ChatHistoryDto>.Success(new ChatHistoryDto
            {
                ChannelType = SocialChatChannelType.Direct,
                OtherUserId = request.OtherUserId,
                Messages = rows.Select(MapEntity).ToList()
            });
        }
    }

    private static ChatMessageDto MapEntity(SocialChatMessageEntity e)
    {
        return new ChatMessageDto
        {
            MessageId = e.Id,
            ChannelType = e.ChannelType,
            TeamId = e.TeamId,
            SenderUserId = e.SenderUserId,
            SenderUsername = e.SenderUser?.Login ?? $"Player {e.SenderUserId}",
            RecipientUserId = e.RecipientUserId,
            RecipientUsername = e.RecipientUser?.Login,
            Body = e.Body,
            SentUtc = e.SentUtc
        };
    }

    private async Task PushChatMessageAsync(ChatMessageDto dto, HashSet<long> targetUserIds)
    {
        NetworkFrame frame = new(MessageFrameBuilder.ChatMessageReceivedFrame(dto));
        foreach (IClient c in clientManager.GetAllLoggedInClients())
        {
            if (c.User is not null && targetUserIds.Contains(c.User.Id))
            {
                try { await c.SendAsync(frame); } catch { }
            }
        }
    }
}
