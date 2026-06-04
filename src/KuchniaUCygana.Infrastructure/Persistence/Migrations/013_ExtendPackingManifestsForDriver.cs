using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

/// <summary>
/// Rozszerza PackingManifests o pola weryfikacji przez użytkownika (ID zamiast tekstowego VerifiedBy)
/// oraz opcjonalny link do kierowcy (M4 przypisuje kierowcę, M3 przechowuje referencję).
/// 
/// UWAGA: Istniejące pole VerifiedBy (string) z migracji 009 jest zachowane dla kompatybilności.
/// VerifiedByUserId (int?) to nowe pole do powiązania z tabelą Users.
/// </summary>
[Migration(13)]
public sealed class ExtendPackingManifestsForDriver : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingManifests").Column("VerifiedByUserId").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("VerifiedByUserId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("DriverUserId").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("DriverUserId").AsInt32().Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingManifests").Column("DriverUserId").Exists())
        {
            Delete.Column("DriverUserId").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("VerifiedByUserId").Exists())
        {
            Delete.Column("VerifiedByUserId").FromTable("PackingManifests");
        }
    }
}
