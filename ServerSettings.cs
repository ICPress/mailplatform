
public class ServerSettings
{
    public string MysqlConnectionGorse { get; set; } = "";
    public string MysqlConnectionStoryPop { get; set; } = "";

    public string JWTSecret { get; set; } = "";

    public string IgniteEndpoint { get; set; } = "";
    public string SpacyEndpoint { get; set; } = "";

    public string GorseAPIEndpoint { get; set; } = "";

    public string SMTPServer { get; set; } = "mail.landermoon.com";
    public int SMTPPort { get; set; } = 465;

    public string SMTPUsername { get; set; } = "username@landermoon.com";
    public string SMTPPassword { get; set; } = "";

     public string SwiftAuthEndpoint { get; set; } = "";

    public string SwiftTempAuthUser { get; set; } = "";
    public string SwiftTempAuthKey { get; set; } = "";
    public string SwiftBucketSmallPath { get; set; } = "";

    public string SwiftBucketLargePath { get; set; } = "";

    public string SwiftBucketUserMessagePath { get; set; } = "";

    public string CDNSmallName { get; set; } = "";

    public string CDNPublishSmallPath { get; set; } = "";
    public string CDNPublishLargePath { get; set; } = "";
    public string CDNPublishUserMessagePath { get; set; } = "";
    public string CDNRequestSmallPath { get; set; } = "";
    public string CDNRequestLargePath { get; set; } = "";
    public string CDNRequestUserMessagePath { get; set; } = "";

    public string FirebaseSDKCredentialsJson { get; set; } = "";

    public string CDNAccessKey { get; set; } = "";

    public string AdminUsername { get; set; } = "";

    public string ImageStaticPath { get; set; } = "";

    public bool RequireArticleSources { get; set; } = false;
    public bool RequireArticleReview { get; set; } = false;
}