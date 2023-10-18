using System.Net.Http.Headers;
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
                type = ModificationQueryType.Change,
                keys = new List<string>
                {
                    "id", "desc"
                },
                predicate = (x) => int.Parse(x[0].ToString()) == 0,
                data = new List<object>
                {
                    1, "Hes cool i guess"
                }
            });

            if (!success)
                Console.Write("Error: Failed to modify table");

            DbClient.Close();
            Console.ReadLine();
        }
    }
}