using System.Net.NetworkInformation;

namespace JsonDb.Tests
{
    internal class Program
    {
        static JsonDbFile DbFile = new JsonDbFile("Production", Environment.CurrentDirectory + "\\prod.jsondb");
        static JsonDbClient DbClient = new JsonDbClient();

        static void Main(string[] args)
        {
            DbClient.Open(DbFile);

            DbClient.InitTable("test", new List<string>
            {
                "id", "name", "desc"
            }, new List<List<object>>
            {
                new List<object>
                {
                    0, "Test", "None"
                }
            });

            bool success = DbClient.ModifyQuery(new ModificationQuery
            {
                table = "test",
                type = ModificationQueryType.Insert,
                data = new List<object>
                {
                    1, "Joe", "Hes cool i guess"
                }
            });

            if (!success)
            {
                Console.Write("Error: Failed to modify table");
            }

            DbClient.Close();
        }
    }
}