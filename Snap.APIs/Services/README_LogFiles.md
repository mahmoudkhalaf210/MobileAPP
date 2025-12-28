# Log Files Location

## Log File Path

After configuring Serilog, log files are written to:

```
Logs/snap-api-YYYYMMDD.log
```

**Full path example:**
- **Windows**: `D:\Mobile app backend\final\Snap.APIs\Logs\snap-api-20241215.log`
- **Relative to project root**: `Logs/snap-api-20241215.log`

## Log File Details

- **Location**: `Logs/` folder in your project root directory
- **File naming**: `snap-api-YYYYMMDD.log` (one file per day)
- **Rolling**: Daily - a new file is created each day
- **Retention**: Keeps last 30 days of logs (older files are automatically deleted)
- **Format**: Includes timestamp, log level, message, and exception details

## Example Log Entry

```
2024-12-15 14:30:25.123 +00:00 [INF] PendingOrderDeletionService is starting. Checking every 1 minute(s) for orders older than 4 minutes.
2024-12-15 14:31:25.456 +00:00 [INF] Found 2 expired pending order(s) to delete.
2024-12-15 14:31:25.789 +00:00 [INF] Successfully deleted 2 expired pending order(s). Order IDs: 123, 124
```

## Log Levels

The service logs at different levels:
- **Information (INF)**: Normal operations, service start/stop, successful deletions
- **Warning (WRN)**: Unexpected situations (e.g., expected to delete but didn't)
- **Error (ERR)**: Exceptions and errors
- **Debug (DBG)**: Detailed diagnostic information (when enabled)

## Viewing Logs

### Option 1: Direct File Access
Navigate to the `Logs` folder in your project directory and open the log file for today's date.

### Option 2: Using a Log Viewer
You can use tools like:
- **Notepad++** (with log viewer plugins)
- **Visual Studio Code** (with log viewer extensions)
- **BareTail** (Windows log tailing tool)
- **LogViewer** (Windows application)

### Option 3: Command Line (Windows)
```powershell
# View today's log
Get-Content Logs\snap-api-$(Get-Date -Format "yyyyMMdd").log -Tail 50

# Follow log in real-time
Get-Content Logs\snap-api-$(Get-Date -Format "yyyyMMdd").log -Wait -Tail 20
```

## Configuration

Logging is configured in:
- **Program.cs**: Serilog setup with file and console output
- **appsettings.json**: Log level configuration

To change log file location or settings, modify `Program.cs`:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/snap-api-.log",  // Change this path
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,  // Change retention days
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

## Important Notes

⚠️ **Logs Directory**: The `Logs` folder will be created automatically when the application starts if it doesn't exist.

⚠️ **Permissions**: Make sure the application has write permissions to the Logs directory.

⚠️ **Disk Space**: Logs can grow over time. The default retention is 30 days, but you can adjust this.

⚠️ **Production**: In production, consider:
- Using a centralized logging solution (e.g., Application Insights, ELK Stack)
- Configuring log rotation and cleanup
- Setting appropriate log levels to reduce verbosity



