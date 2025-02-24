using FluentMigrator;

[Migration(2024051603)]
public class CreateGameTournamentTable : Migration
{
    public override void Up()
    {
        Create.Table("GameTournament")
            .WithColumn("tournament_id").AsInt64().NotNullable().ForeignKey("Tournaments", "id")
            .WithColumn("game_id").AsInt64().NotNullable().ForeignKey("Games", "id");
    }

    public override void Down()
    {
        Delete.Table("GameTournament");
    }
}
