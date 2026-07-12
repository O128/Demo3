using MySqlConnector;
using Serilog;

namespace 点胶机.Data;

/// <summary>
/// 数据库初始化器 —— 自动建库建表
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// 初始化:解析连接串 → 建库(若不存在)→ 建表
    /// 返回是否成功(失败时上层降级为仅文件日志)
    /// </summary>
    public static bool Initialize(string connectionString, bool autoCreateTable)
    {
        try
        {
            // 解析连接串,分离出数据库名
            var csb = new MySqlConnectionStringBuilder(connectionString);
            var dbName = csb.Database;
            if (string.IsNullOrEmpty(dbName))
            {
                Log.Warning("[DB] 连接串未指定数据库名,跳过建库");
                dbName = "dispenser";
            }

            // 1. 连接 MySQL 服务器(不指定库),创建数据库
            csb.Database = "";
            using (var conn = new MySqlConnection(csb.ConnectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                cmd.ExecuteNonQuery();
                Log.Information("[DB] 数据库 {Db} 已就绪", dbName);
            }

            // 2. 连接目标库,建表
            if (autoCreateTable)
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
                CreateTables(conn);
                Log.Information("[DB] 数据表已就绪");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DB] 数据库初始化失败,系统将降级为仅文件日志");
            return false;
        }
    }

    private static void CreateTables(MySqlConnection conn)
    {
        // 日志表
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS `Logs` (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `Timestamp` DATETIME NOT NULL,
                `Level` VARCHAR(16) NOT NULL,
                `Module` VARCHAR(64) NULL,
                `Message` TEXT NOT NULL,
                `Exception` TEXT NULL,
                `MachineName` VARCHAR(64) NULL,
                PRIMARY KEY (`Id`),
                INDEX `IX_Logs_Timestamp` (`Timestamp`),
                INDEX `IX_Logs_Level` (`Level`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        // 报警历史表
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS `Alarms` (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `AlarmId` INT NOT NULL,
                `AlarmName` VARCHAR(128) NOT NULL,
                `Level` VARCHAR(16) NOT NULL,
                `StartTime` DATETIME NOT NULL,
                `EndTime` DATETIME NULL,
                `DurationSec` DOUBLE NULL,
                `AckTime` DATETIME NULL,
                `AckUser` VARCHAR(32) NULL,
                PRIMARY KEY (`Id`),
                INDEX `IX_Alarms_StartTime` (`StartTime`),
                INDEX `IX_Alarms_Level` (`Level`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        // 生产记录表
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS `Production` (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `StartTime` DATETIME NOT NULL,
                `EndTime` DATETIME NULL,
                `CycleTime` DOUBLE NULL,
                `Result` VARCHAR(8) NOT NULL DEFAULT 'OK',
                `RecipeName` VARCHAR(64) NULL,
                `OffsetX` DOUBLE NOT NULL DEFAULT 0,
                `OffsetY` DOUBLE NOT NULL DEFAULT 0,
                `GlueAmount` DOUBLE NOT NULL DEFAULT 0,
                PRIMARY KEY (`Id`),
                INDEX `IX_Production_StartTime` (`StartTime`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);
    }

    private static void Execute(MySqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
