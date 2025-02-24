using FluentMigrator;

[Migration(2024030202)]
public class CreateRanksTable : Migration
{
    public override void Up()
    {
        Create.Table("Ranks")
            .WithColumn("id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("min_points").AsInt32().NotNullable()
            .WithColumn("max_points").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Ranks");
    }
}
