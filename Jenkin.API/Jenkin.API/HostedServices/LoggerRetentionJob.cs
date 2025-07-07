using Microsoft.Data.SqlClient;
using Quartz;
using Serilog;

namespace Jenkin.API.HostedServices
{
    /// <summary>
    /// LoggerRetentionJob is a job to delete old log in database.
    /// Normally set for 90 days retention before delete old log.
    /// </summary>
    public class LoggerRetentionJob : IJob
    {
        // Connection String.
        private readonly string _connectionString;

        // Priod of retention time in days.
        private readonly int _retentionTime;

        public LoggerRetentionJob(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _retentionTime = configuration.GetValue<int>("Serilog:WriteTo:2:Args:sinkOptionsSection:retainedPeriod", 90);
        }

        public async Task Execute(IJobExecutionContext context)
        {

            await using var connection = new SqlConnection(_connectionString);
            try
            {
                Log.Information("{Services} Delete log out of retention start {datetime}", nameof(LoggerRetentionJob), DateTime.Now);

                await connection.OpenAsync();

                var retentionDatetime = DateTime.Now.AddDays(-1 * _retentionTime);
                Log.Verbose("RetentionTime: {retentionDatetime}", retentionDatetime);

                int totalRowsDeleted = 0;
                int batchSize = 10000; // Adjust based on your database performance
                int rowsDeletedInBatch;

                do
                {
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        var sqlCommand = new SqlCommand(
                            "DELETE TOP (@BatchSize) FROM [EventLogging].[Logs] WHERE [TimeStamp] < @RetentionTime;",
                            connection,
                            transaction);

                        sqlCommand.Parameters.Add("@BatchSize", System.Data.SqlDbType.Int).Value = batchSize;
                        sqlCommand.Parameters.Add("@RetentionTime", System.Data.SqlDbType.DateTime).Value = retentionDatetime;

                        rowsDeletedInBatch = await sqlCommand.ExecuteNonQueryAsync();
                        totalRowsDeleted += rowsDeletedInBatch;

                        await transaction.CommitAsync();
                        Log.Debug("{Services} Batch deleted {rowsDeletedInBatch} rows", nameof(LoggerRetentionJob), rowsDeletedInBatch);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Log.Error(ex, "{Services} Error deleting logs batch.", nameof(LoggerRetentionJob));
                        throw;
                    }

                    // Optional: Add a small delay between batches to reduce database load
                    if (rowsDeletedInBatch > 0)
                        await Task.Delay(100);

                } while (rowsDeletedInBatch > 0);

                Log.Information("{Service} Delete log out of retention completed [From={RetentionTime},Deleted={NumberOfRows}]",
                    nameof(LoggerRetentionJob), retentionDatetime, totalRowsDeleted);

            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Services} Delete log out of retention failed.", nameof(LoggerRetentionJob));
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}
