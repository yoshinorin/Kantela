using Kantela.Core.Services.Web;

namespace Kantela.Core.Tests.Services.Web;

[TestClass]
public sealed class HttpWebClientTests
{
    [TestMethod]
    public void CreateHttpClient_SendsProductAndRepositoryInUserAgent()
    {
        using HttpClient client = HttpWebClient.CreateHttpClient("1.2.3");

        Assert.AreEqual(
            "Kantela/1.2.3 (+https://github.com/yoshinorin/Kantela)",
            client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.AreEqual(TimeSpan.FromSeconds(10), client.Timeout);
    }
}
