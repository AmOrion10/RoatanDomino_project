using FluentMigrator;

[Migration(2024030309)]
public class CreateUserTournamentTable : Migration
{
    public override void Up()
    {
        Create.Table("UserTournament")
            .WithColumn("tournament_id").AsInt64().NotNullable().ForeignKey("Tournaments", "id")
            .WithColumn("user_id").AsInt64().NotNullable().ForeignKey("Users", "id");
    }

    public override void Down()
    {
        Delete.Table("UserTournament");
    }
}
