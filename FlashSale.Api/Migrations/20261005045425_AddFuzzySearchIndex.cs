using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashSale.Api.Migrations;

public partial class AddFuzzySearchIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Enable the pg_trgm extension in the PostgreSQL database
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        // 2. Create a GIN index on Name and Description for fast fuzzy searching
        // We use gin_trgm_ops to tell PostgreSQL to index the trigrams of these text columns
        migrationBuilder.Sql(
            "CREATE INDEX ix_flashsales_name_description_trgm ON \"FlashSales\" USING gin (\"Name\" gin_trgm_ops, \"Description\" gin_trgm_ops);"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse the index creation
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_flashsales_name_description_trgm;");
        
        // Note: We do not drop the extension in Down() as other tables might rely on it
    }
}