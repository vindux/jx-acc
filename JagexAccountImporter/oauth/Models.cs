using System.Text.Json.Serialization;

namespace JagexAccountImporter.oauth;

public class Character
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    public Character() { }

    public Character(string displayName, string accountId)
    {
        DisplayName = displayName;
        AccountId = accountId;
    }

    public override string ToString()
    {
        return $"{{ \"displayName\": \"{DisplayName}\", \"accountId\": \"{AccountId}\"}}";
    }
}

public class LoginToken(string sessionId, Character[] characters)
{
    public string SessionId { get; } = sessionId;
    public Character[] Characters { get; } = characters;

    public override string ToString()
    {
        return $"LoginToken: sessionId: {SessionId} characters: [{string.Join(", ", (IEnumerable<Character>)Characters)}]";
    }
}

public class OAuth2Response(string code, string idToken)
{
    public string Code { get; } = code;
    public string IdToken { get; } = idToken;
}