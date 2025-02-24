using FluentMigrator;

[Migration(2024030200)]
public class CreateAvatarTable : Migration
{
    public override void Up()
    {
        Create.Table("Avatar")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("image").AsCustom("BLOB").NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Avatar");
    }
}
