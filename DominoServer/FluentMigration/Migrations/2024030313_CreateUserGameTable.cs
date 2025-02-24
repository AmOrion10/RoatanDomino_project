using System.Data;

using FluentMigrator;

[Migration(2024030313)]
public class CreateUserGameTable : Migration
{
    public override void Up()
    {
        Create.Table("UserGame")
            .WithColumn("user_id").AsInt64().NotNullable().PrimaryKey().ForeignKey("Users", "id").OnDeleteOrUpdate(Rule.Cascade)
            .WithColumn("game_id").AsInt64().NotNullable().PrimaryKey().ForeignKey("Games", "id").OnDeleteOrUpdate(Rule.Cascade)
            .WithColumn("score_update").AsInt16().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("UserGame");
    }
}
