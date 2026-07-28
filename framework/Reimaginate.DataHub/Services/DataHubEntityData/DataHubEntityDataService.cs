using Newtonsoft.Json.Linq;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Cosmos;

namespace Reimaginate.DataHub.Services.DataHubEntityData;

public interface IDataHubEntityDataService : IPartitionedDataService<JObject>
{ }

public class DataHubEntityDataService(CosmosDataServiceOptions<JObject> options) : CosmosDataService<JObject>(options), IDataHubEntityDataService;