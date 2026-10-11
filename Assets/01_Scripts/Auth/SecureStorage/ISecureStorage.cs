public interface ISecureStorage
{
    bool HasRefreshToken();

    bool SaveRefreshToken(string refreshToken);

    string GetRefreshToken();

    bool DeleteRefreshToken();
}