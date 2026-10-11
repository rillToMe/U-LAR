using System;

[Serializable]
public class StudentLoginRequest
{
    public string nim;
    public string password;
}

[Serializable]
public class StudentLoginResponse
{
    public int userId;
    public string nim;
    public string name;
    public string email;
    public string role;
    public string accessToken;
    public string refreshToken;
}

[Serializable]
public class RefreshTokenRequest
{
    public string refreshToken;
}

[Serializable]
public class RefreshTokenResponse
{
    public string accessToken;
    public string refreshToken;
}