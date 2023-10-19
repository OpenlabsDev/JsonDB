using System.Net.Http.Headers;
using System.Net.NetworkInformation;

namespace JsonDb.Tests
{
    internal class Program
    {
        static JsonDbFile DbFile = new JsonDbFile("Testing Database", Environment.CurrentDirectory + "\\test.jsondb");
        static JsonDbClient DbClient = new JsonDbClient(DbFile);

        static void Main(string[] args)
        {
            var session = DbClient.Open("customSessionId");
            session.InitTable(new InitTableArgs("jdb.test", new List<string>
            {
                "guid"
            }, new List<List<object>>
            {
                new List<object>
                {
                    Guid.NewGuid().ToString(),
                },
                new List<object>
                {
                    Guid.NewGuid().ToString(),
                },
                new List<object>
                {
                    Guid.NewGuid().ToString(),
                },
                new List<object>
                {
                    Guid.NewGuid().ToString(),
                }
            })
            {
                OnSuccess = () =>
                {
                    Console.WriteLine("Successfully initialized table");
                },
                OnError = (err) =>
                {
                    Console.Error.WriteLine("Failed to init table: " + err);
                }
            });

            DbClient.InitTable(new InitTableArgs("jdb.test2", new List<string>
            {
                "key1", "key2", "key3"
            }, new List<List<object>>
            {
                new List<object>
                {
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999)
                },
                new List<object>
                {
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999)
                },
                new List<object>
                {
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999)
                },
                new List<object>
                {
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999)
                },
                new List<object>
                {
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999)
                },
                new List<object>
                {
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999),
                    Random.Shared.Next(1000, 19999999)
                }
            }), () =>
            {
                Console.WriteLine("Successfully initialized table sync");
            }, err =>
            {
                Console.Error.WriteLine("Failed to init table sync: " + err);
            });

            Console.ReadLine();
        }
    }
}