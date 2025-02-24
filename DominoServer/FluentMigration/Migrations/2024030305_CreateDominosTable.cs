using FluentMigrator;

[Migration(2024030305)]
public class CreateDominosTable : Migration
{
    public override void Up()
    {
        Create.Table("Dominos")
            .WithColumn("id").AsInt16().NotNullable().PrimaryKey().Identity()
            .WithColumn("side_one").AsInt16().NotNullable()
            .WithColumn("side_two").AsInt16().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Dominos");
    }
}
