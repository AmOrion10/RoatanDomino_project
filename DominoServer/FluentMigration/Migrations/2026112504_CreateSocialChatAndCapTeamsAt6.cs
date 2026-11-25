using FluentMigrator;

namespace FluentMigration.Migrations;

[Migration(2026112504)]
public class CreateSocialChatAndCapTeamsAt6 : Migration
{
    public override void Up()
    {
        if (Schema.Table("Teams").Column("max_members").Exists())
        {
            Execute.Sql("UPDATE Teams SET max_members = 6 WHERE max_members > 6;");
        }

        if (!Schema.Table("SocialChatMessages").Exists())
        {
            Create.Table("SocialChatMessages")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("channel_type").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("team_id").AsInt64().Nullable()
                .WithColumn("sender_user_id").AsInt64().NotNullable()
                .WithColumn("recipient_user_id").AsInt64().Nullable()
                .WithColumn("body").AsString(1000).NotNullable()
                .WithColumn("sent_utc").AsDateTime().NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("SocialChatMessages").Exists())
        {
            Delete.Table("SocialChatMessages");
        }
    }
}
