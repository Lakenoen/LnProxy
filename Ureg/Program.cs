using IndexModule;
using Ureg;

public class Program
{
    private const string IndexFileName = "Users.index";
    public static void Main(string[] args)
    {
        Methods? methods = null;
        try
        {
            if (args.Length == 0)
                throw new ArgumentException("Enter the command, to view commands - help");

            methods = new Methods(IndexFileName);

            string command = args[0];
            args = args.Take(1..).ToArray();
            switch (command.ToLower())
            {
                case "help":
                    {
                        Console.WriteLine("All commands:");
                        Console.WriteLine("create - create user, ( Ureg create <login> <password> )");
                        Console.WriteLine("remove - remove user, ( Ureg remove <login> )");
                        Console.WriteLine("search - search user password, ( Ureg search <login> )");
                        Console.WriteLine("all - print all users, ( Ureg all )");
                        Console.WriteLine("clear - clear deleted nodes, link to cron, ( all )");
                        Console.WriteLine("help - print information, ( Ureg help )");
                        Console.WriteLine("path - print path of index file, ( Ureg path )");
                    }
                    ;break;
                case "path": Console.WriteLine( $"Authorization file - \"{Directory.GetCurrentDirectory()}\\{IndexFileName}\"" ); break;
                case "create": methods.Create(args); break;
                case "remove": methods.Remove(args); break;
                case "search": Console.WriteLine(methods.Search(args) ); break;
                case "clear": methods.ClearDeletedNote(); break;
                case "all": {
                        var list = methods.GetAll();
                        foreach (var item in list)
                        {
                            Console.WriteLine( $"{item.key} {item.value}" );
                        }
                    }; break;
                default: throw new ArgumentException("Unknown command, look up the commands with ( Ureg help )");
            }

            Console.WriteLine("Complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            methods?.Dispose();
        }
    }
}