using FluentMigrator;

[Migration(2024030302)]
public class CreateTournamentsTable : Migration
{
    public override void Up()
    {
        Create.Table("Tournaments")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("start_date").AsDateTime().NotNullable()
            .WithColumn("end_date").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Tournaments");
    }
}
