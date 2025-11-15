using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDesk.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentsUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'StorageKey'
        );
        SET @sql := IF(@exists = 0,
            'ALTER TABLE `Attachments` ADD COLUMN `StorageKey` VARCHAR(1000) NULL AFTER `SizeBytes`;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        SET @sp_exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'StoragePath'
        );
        SET @sql := IF(@sp_exists > 0,
            'UPDATE `Attachments` SET `StorageKey` = `StoragePath` WHERE `StorageKey` IS NULL;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        UPDATE `Attachments`
        SET `StorageKey` = CONCAT('missing/', `Id`, '_unknown')
        WHERE `StorageKey` IS NULL;
    ");
            migrationBuilder.Sql(@"
        ALTER TABLE `Attachments`
        MODIFY COLUMN `StorageKey` VARCHAR(1000) NOT NULL;
    ");

            migrationBuilder.Sql(@"
        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'PublicUrl'
        );
        SET @sql := IF(@exists = 0,
            'ALTER TABLE `Attachments` ADD COLUMN `PublicUrl` VARCHAR(2000) NULL AFTER `StorageKey`;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'StoragePath'
        );
        SET @sql := IF(@exists > 0,
            'ALTER TABLE `Attachments` DROP COLUMN `StoragePath`;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE `Tickets`
        MODIFY COLUMN `CategoryId` INT NULL;
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'StoragePath'
        );
        SET @sql := IF(@exists = 0,
            'ALTER TABLE `Attachments` ADD COLUMN `StoragePath` VARCHAR(1000) NULL AFTER `SizeBytes`;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'StorageKey'
        );
        SET @sql := IF(@exists > 0,
            'UPDATE `Attachments` SET `StoragePath` = `StorageKey` WHERE `StorageKey` IS NOT NULL;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'PublicUrl'
        );
        SET @sql := IF(@exists > 0,
            'ALTER TABLE `Attachments` DROP COLUMN `PublicUrl`;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

        SET @exists := (
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Attachments'
              AND COLUMN_NAME = 'StorageKey'
        );
        SET @sql := IF(@exists > 0,
            'ALTER TABLE `Attachments` DROP COLUMN `StorageKey`;',
            'SELECT 1;'
        );
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE `Tickets`
        MODIFY COLUMN `CategoryId` INT NULL;
    ");
        }
    }
}
