namespace NOF.Hosting.AspNetCore;

public class CorsSettingsOptions
{
    public string[] AllowedOrigins { get; set; } = [];

    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"];

    public string[] AllowedHeaders { get; set; } = ["*"];

    public bool AllowCredentials { get; set; } = true;
};
