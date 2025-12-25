using Dapper;
using Npgsql;  // Namespace do PostgreSQL
using System.Data;
using Microsoft.Data.Sqlite; // para SQLite
using System.Threading.Tasks;

public class EmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Método para buscar todos os empregados
    public async Task<IEnumerable<PersonEntity>> GetEmployeesAsync(string filter)
    {
        try
        {
            using (IDbConnection dbConnection = new NpgsqlConnection(_connectionString))
            {
                dbConnection.Open();
                var employees = await dbConnection.QueryAsync<PersonEntity>(filter);
                return employees;
            } 
        }
        catch (Exception ex)
        {
            using (IDbConnection dbConnection = new SqliteConnection(_connectionString))
            {
                dbConnection.Open();
                var employees = await dbConnection.QueryAsync<PersonEntity>(filter);
                return employees;
            }
        }
    }
}
