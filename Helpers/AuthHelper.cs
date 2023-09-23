using System.Text;

namespace CodeGenerator.Helpers
{
    public class AuthHelper
    {
        public static string GetBasicAuthTokenFromUserPass(string username, string password)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");

            return Convert.ToBase64String(byteArray);
        }
    }
}
