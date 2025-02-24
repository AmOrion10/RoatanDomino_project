using System.Data;
using FluentMigrator;

[Migration(2024030304)]
public class CreateGamesTable : Migration
{
    public override void Up()
    {
        Create.Table("Games")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("start_time").AsDateTime().NotNullable()
            .WithColumn("end_time").AsDateTime().NotNullable()
            .WithColumn("mode").AsString().NotNullable()
            .WithColumn("bet").AsInt16().Nullable()
            .WithColumn("winner_id").AsInt64().Nullable().ForeignKey("Users", "id").OnDeleteOrUpdate(Rule.SetNull)
            .WithColumn("game_duration").AsInt16().NotNullable()
            .WithColumn("turn_duration").AsInt16().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Games");
    }
}
