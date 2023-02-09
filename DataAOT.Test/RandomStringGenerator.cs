using System.Text;

namespace DataAOT.Test;

public static class RandomStringGenerator
{
    private static readonly Random Random = new ();
    
    /// <summary>
    /// Generate a random string with length in specified range
    /// </summary>
    /// <returns></returns>
    public static string Create(int minLength = 8, int maxLength = 10)
    {
        var length = Random.Next(minLength, maxLength);
        var str = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            str.Append(Convert.ToChar(Random.Next(0, 26) + 65));
        }
        return str.ToString();
    }
    

}