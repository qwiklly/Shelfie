using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShelfieBackend.Migrations
{
    /// <summary>
    /// Copies data from the old schema (Products, Medications, custom Categories with EAV values, HistoryRecords)
    /// into Items / ItemCategories / HistoryEntries. Ids are derived deterministically (md5 of the old id), so
    /// history entries keep pointing to the migrated items.
    ///
    /// Custom categories: the first field (lowest FieldOrder) becomes the item name; the other fields become
    /// typed fields with keys "f{FieldOrder}". The old predefined "Книги" row is dropped — books are built-in now.
    /// </summary>
    public partial class UnifiedInventory
    {
        private const string ProductsId = "00000000-0000-0000-0000-000000000001";
        private const string MedicationsId = "00000000-0000-0000-0000-000000000002";

        private static void MigrateData(MigrationBuilder migrationBuilder)
        {
            const string itemColumns = """
                "UserId", "Id", "CategoryId", "Name", "Brand", "Notes", "Quantity", "Weight", "WeightUnit",
                "ExpirationDate", "Location", "Barcode", "Fields", "CreatedAt", "UpdatedAt", "ServerModifiedAt", "IsDeleted"
                """;

            // Products and medications.
            foreach (var (table, prefix, categoryId) in new[]
                     {
                         ("Products", "shelfie-product-", ProductsId),
                         ("Medications", "shelfie-medication-", MedicationsId)
                     })
            {
                migrationBuilder.Sql($$"""
                    INSERT INTO "Items" ({{itemColumns}})
                    SELECT p."UserId", md5('{{prefix}}' || p."Id")::uuid, '{{categoryId}}'::uuid,
                           LEFT(p."Name", 200), NULLIF(p."Creator", ''), NULLIF(p."Description", ''),
                           GREATEST(p."Quantity", 0), p."Weight", NULLIF(p."WeightUnit", ''),
                           p."ExpirationDate", NULL, NULL, '{}'::jsonb,
                           p."CreatedAt"::timestamp AT TIME ZONE 'UTC', now(), now(), false
                    FROM "{{table}}" p;
                    """);
            }

            // Custom categories: all fields but the first become typed text fields.
            migrationBuilder.Sql("""
                INSERT INTO "ItemCategories" ("UserId", "Id", "Name", "Kind", "Fields", "UpdatedAt", "ServerModifiedAt", "IsDeleted")
                SELECT c."UserId", md5('shelfie-category-' || c."Id")::uuid, LEFT(c."Name", 100), 'Custom',
                       COALESCE((
                           SELECT jsonb_agg(jsonb_build_object(
                                      'key', 'f' || f."FieldOrder", 'name', f."FieldName", 'type', 'Text',
                                      'required', false, 'order', f."FieldOrder")
                                  ORDER BY f."FieldOrder")
                           FROM "CategoryFields" f
                           WHERE f."CategoryId" = c."Id"
                             AND f."FieldOrder" > (SELECT MIN(f2."FieldOrder") FROM "CategoryFields" f2 WHERE f2."CategoryId" = c."Id")
                       ), '[]'::jsonb),
                       now(), now(), false
                FROM "Categories" c
                WHERE c."UserId" IS NOT NULL AND c."Type" = 'Custom';
                """);

            // Records of custom categories (EAV rows grouped by RecordId) become items.
            migrationBuilder.Sql($$"""
                INSERT INTO "Items" ({{itemColumns}})
                SELECT c."UserId",
                       md5('shelfie-record-' || v."CategoryId" || '-' || v."RecordId")::uuid,
                       md5('shelfie-category-' || v."CategoryId")::uuid,
                       LEFT(COALESCE(NULLIF(MAX(CASE WHEN v."FieldOrder" = fo.min_order THEN v."Value" END), ''),
                                     'Запись ' || v."RecordId"), 200),
                       NULL, NULL, 1, NULL, NULL, NULL, NULL, NULL,
                       COALESCE(jsonb_object_agg('f' || v."FieldOrder", v."Value")
                                FILTER (WHERE v."FieldOrder" <> fo.min_order AND f."Id" IS NOT NULL), '{}'::jsonb),
                       now(), now(), now(), false
                FROM "CategoryFieldValues" v
                JOIN "Categories" c ON c."Id" = v."CategoryId" AND c."UserId" IS NOT NULL AND c."Type" = 'Custom'
                JOIN (SELECT "CategoryId", MIN("FieldOrder") AS min_order FROM "CategoryFields" GROUP BY "CategoryId") fo
                     ON fo."CategoryId" = v."CategoryId"
                LEFT JOIN "CategoryFields" f ON f."CategoryId" = v."CategoryId" AND f."FieldOrder" = v."FieldOrder"
                GROUP BY c."UserId", v."CategoryId", v."RecordId", fo.min_order;
                """);

            // The first migration created "admin@example.com" with a password published in the repository.
            // Disable the password of that exact seeded account (an empty hash never verifies); data is kept.
            migrationBuilder.Sql("""
                UPDATE "Users" SET "PasswordHash" = ''
                WHERE "Email" = 'admin@example.com' AND "Name" = 'Admin'
                  AND "Phone" = '1234567890' AND "DateOfBirth" = DATE '2001-01-01';
                """);

            // History.
            migrationBuilder.Sql($"""
                INSERT INTO "HistoryEntries" ("UserId", "Id", "ItemId", "ItemName", "CategoryId", "ChangeType",
                                              "QuantityChange", "ChangeDate", "UpdatedAt", "ServerModifiedAt", "IsDeleted")
                SELECT h."UserId", md5('shelfie-history-' || h."Id")::uuid,
                       CASE h."ItemType"
                           WHEN 'Product' THEN md5('shelfie-product-' || h."ItemId")::uuid
                           WHEN 'Medicament' THEN md5('shelfie-medication-' || h."ItemId")::uuid
                       END,
                       h."ItemName",
                       CASE h."ItemType"
                           WHEN 'Product' THEN '{ProductsId}'::uuid
                           WHEN 'Medicament' THEN '{MedicationsId}'::uuid
                       END,
                       LEFT(CASE h."ChangeType" WHEN 'Deleted' THEN 'Removed' ELSE h."ChangeType" END, 20),
                       h."QuantityChange", h."ChangeDate", now(), now(), false
                FROM "HistoryRecords" h;
                """);
        }
    }
}
