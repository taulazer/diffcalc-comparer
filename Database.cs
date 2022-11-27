using MySqlConnector;

namespace diffcalc_comparer;

public static class Database
{
    public static async Task<MySqlConnection> GetConnection()
    {
        var connection = new MySqlConnection("Server=db;User ID=root;ConnectionTimeout=5;ConnectionReset=false;Pooling=true;");
        await connection.OpenAsync();
        return connection;
    }
}
