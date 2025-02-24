using FluentMigrator;

[Migration(2024030314)]
public class CreateMovesTable : Migration
{
    public override void Up()
    {
        Create.Table("Moves")
            .WithColumn("id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("row").AsInt16().NotNullable()
            .WithColumn("col").AsInt16().NotNullable()
            .WithColumn("orientation").AsString().NotNullable()
            .WithColumn("order").AsInt16().NotNullable()
            .WithColumn("domino_id").AsInt16().NotNullable().ForeignKey("Dominos", "id")
            .WithColumn("player_id").AsInt64().Nullable().ForeignKey("Users", "id").OnDeleteOrUpdate(System.Data.Rule.SetNull)
            .WithColumn("game_id").AsInt64().NotNullable().ForeignKey("Games", "id").OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Table("Moves");
    }
}
