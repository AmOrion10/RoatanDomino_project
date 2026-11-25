using FluentMigrator;

[Migration(2026112501)]
public class AddEloRankingColumnsToUsers : Migration
{
    public override void Up()
    {
        Alter.Table("Users")
            .AddColumn("elo").AsInt32().NotNullable().WithDefaultValue(1000)
            .AddColumn("wins").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("losses").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("win_assists").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("total_moves").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("optimal_moves").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("average_turn_seconds").AsDouble().NotNullable().WithDefaultValue(0.0)
            .AddColumn("online_matches_last_7_days").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("last_online_match_utc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("elo").FromTable("Users");
        Delete.Column("wins").FromTable("Users");
        Delete.Column("losses").FromTable("Users");
        Delete.Column("win_assists").FromTable("Users");
        Delete.Column("total_moves").FromTable("Users");
        Delete.Column("optimal_moves").FromTable("Users");
        Delete.Column("average_turn_seconds").FromTable("Users");
        Delete.Column("online_matches_last_7_days").FromTable("Users");
        Delete.Column("last_online_match_utc").FromTable("Users");
    }
}
