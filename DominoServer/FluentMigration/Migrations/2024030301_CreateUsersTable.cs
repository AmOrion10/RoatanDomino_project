using FluentMigrator;

[Migration(2024030301)]
public class CreateUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("login").AsString().NotNullable().Unique()
            .WithColumn("password").AsString().NotNullable()
            .WithColumn("email").AsString().NotNullable().Unique()
            .WithColumn("date_signup").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("date_last_login").AsDateTime().Nullable()
            .WithColumn("avatar").AsInt64().Nullable().ForeignKey("Avatar", "id")
            .WithColumn("score").AsInt32().NotNullable().WithDefaultValue(10000)
            .WithColumn("birthday").AsDate().NotNullable()
            .WithColumn("nationality").AsInt64().Nullable().ForeignKey("Nationality", "id")
            .WithColumn("rank").AsInt32().Nullable().ForeignKey("Ranks", "id");
    }

    public override void Down()
    {
        Delete.Table("Users");
    }
}
