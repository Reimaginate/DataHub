using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Services.AzureManagement;

public interface IAzureManagementService
{
    Task<List<AgentPublishProfile>> GetPublishProfiles(string functionName, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken);
}

public class AzureManagementService(string subscription, string group, string key) : IAzureManagementService
{
    public async Task<List<AgentPublishProfile>> GetPublishProfiles(string functionName, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("GetAgentPublishProfileQueryHandler");
        string apiVersion = "2021-03-01";

        var url =
            $"https://management.azure.com/subscriptions/{subscription}/resourceGroups/{group}/providers/Microsoft.Web/sites/{functionName}/publishxml?api-version={apiVersion}";

        var httpRequest = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(url)
        };

        httpRequest.Headers.Add("Authorization", $"Bearer {key}");

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        XDocument xmlResponse = XDocument.Parse(responseContent);


        var profs = xmlResponse.Descendants("publishProfile");

        var profList = new List<AgentPublishProfile>();
        foreach (var prof in profs)
        {
            profList.Add(new AgentPublishProfile()
            {
                ProfileName = prof.Attribute("profileName").Value,
                PublishMethod = prof.Attribute("publishMethod").Value,
                PublishUrl = prof.Attribute("publishUrl").Value,
                UserName = prof.Attribute("userName").Value,
                UserPwd = prof.Attribute("userPWD").Value
            });
        }
        return profList;
    }
}
