using FluentMigrator;

namespace FluentMigration.Migrations;

[Migration(2026112505)]
public class AddOpenBoardChallengesAndAbandonment : Migration
{
    public override void Up()
    {
        if (Schema.Table("Users").Exists() && !Schema.Table("Users").Column("abandoned_matches").Exists())
        {
            Alter.Table("Users")
                .AddColumn("abandoned_matches").AsInt32().NotNullable().WithDefaultValue(0);
        }

        if (Schema.Table("TeamChallenges").Exists())
        {
            if (Schema.Table("TeamChallenges").Column("target_team_id").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AlterColumn("target_team_id").AsInt64().Nullable();
            }
            if (!Schema.Table("TeamChallenges").Column("is_open_board").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("is_open_board").AsBoolean().NotNullable().WithDefaultValue(false);
            }
            if (!Schema.Table("TeamChallenges").Column("min_opponent_team_elo").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("min_opponent_team_elo").AsInt32().NotNullable().WithDefaultValue(100);
            }
            if (!Schema.Table("TeamChallenges").Column("require_full_human_squad").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("require_full_human_squad").AsBoolean().NotNullable().WithDefaultValue(false);
            }
            if (!Schema.Table("TeamChallenges").Column("expires_utc").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("expires_utc").AsDateTime().Nullable();
            }
            if (!Schema.Table("TeamChallenges").Column("play_deadline_utc").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("play_deadline_utc").AsDateTime().Nullable();
            }
            if (!Schema.Table("TeamChallenges").Column("team_a_checked_in").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("team_a_checked_in").AsBoolean().NotNullable().WithDefaultValue(false);
            }
            if (!Schema.Table("TeamChallenges").Column("team_b_checked_in").Exists())
            {
                Alter.Table("TeamChallenges")
                    .AddColumn("team_b_checked_in").AsBoolean().NotNullable().WithDefaultValue(false);
            }
        }

        if (!Schema.Table("TeamChallengeEndorsements").Exists())
        {
            Create.Table("TeamChallengeEndorsements")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("challenge_id").AsInt64().NotNullable()
                .WithColumn("team_id").AsInt64().NotNullable()
                .WithColumn("user_id").AsInt64().NotNullable()
                .WithColumn("username").AsString(64).NotNullable()
                .WithColumn("created_utc").AsDateTime().NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("TeamChallengeEndorsements").Exists())
        {
            Delete.Table("TeamChallengeEndorsements");
        }
    }
}
