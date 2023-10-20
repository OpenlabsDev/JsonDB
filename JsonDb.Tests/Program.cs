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
            Console.ReadLine();
        }
    }
}