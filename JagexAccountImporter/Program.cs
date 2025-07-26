using JagexAccountImporter.oauth;

namespace JagexAccountImporter;

internal abstract class Program
{
    private static async Task Main(string[] args)
    {
        AccountService service = new AccountService();

        try
        {
            await service.StartServerAsync();
            Console.WriteLine("HTTP server started on port 80.");

            LoginToken? loginToken = await service.RequestLoginTokenAsync();

            if (loginToken == null)
            {
                Console.WriteLine("Login failed or was cancelled.");
                return;
            }

            Console.WriteLine("Login successful!");
            Console.WriteLine($"Found {loginToken.Characters.Length} character(s):");
            Console.WriteLine();
            Console.WriteLine("JX_SESSEION_ID, JX_CHARACTER_NAME, JX_ACCOUNT_ID");

            foreach (Character character in loginToken.Characters)
            {
                Console.WriteLine($"{loginToken.SessionId}, {character.DisplayName}, {character.AccountId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during login process: {ex.Message}");
        }
        finally
        {
            service.ShutdownServer();
            Console.WriteLine("HTTP server stopped.");
        }
    }
}