using FluentMigrator;

[Migration(2026112503)]
public class CreateTeamChallengesTable : Migration
{
    public override void Up()
    {
        Create.Table("TeamChallenges")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("challenger_team_id").AsInt64().NotNullable().ForeignKey("Teams", "id")
            .WithColumn("target_team_id").AsInt64().NotNullable().ForeignKey("Teams", "id")
            .WithColumn("game_mode").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("variant").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("table_id").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("table_code").AsString(32).NotNullable().WithDefaultValue("")
            .WithColumn("status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_utc").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("TeamChallenges");
    }
}
