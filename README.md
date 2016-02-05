# Service Fabric sample: Reliable Collection Query
Service Fabric currently doesn't provide a way to browse the data in reliable collections. This sample provides a way to get reliable collection data as JSON and dump them into files. You can use the same interface to build a web app that allows browsing collection data (just be sure to secure it properly).

The project contains a simple implementation of this interface for any stateful reliable service:

```csharp
public interface IReliableServiceQuery : IService
{
    Task<IList<Uri>> GetCollections();
    Task<IList<string>> GetCollectionData(Uri name);
}
```

Note that it currently uses Reflection with non-public APIs since `ReliableStateManager.GetEnumerator()` currently throws a `NotImplementedException`.

There is also a project called `ReliableCollectionDump` that can run on the cluster's VM and dump entire collections into JSON files.
For each partition, a file is created with the name `AppName_ServiceName_PartitionId.json`. The output looks like this:

```json
{
  "serviceName": "fabric:/ReliableCollectionQueryApp/SampleStatefulService",
  "partitionId": "4ad044a4-0f1f-442e-976c-b67b6f9d3ec8",
  "collections": [
    {
      "name": "urn:MyState",
      "values": [
        {
          "Key": "f6ed9930-b517-4399-bb0d-6da0fc9e4374",
          "Value": {
            "S": "Test_59de321d-c968-4c05-8cbb-88bdac3398d3",
            "I": 536667375,
            "G": "4ad044a4-0f1f-442e-976c-b67b6f9d3ec8"
          }
        },
        {
          "Key": "cd01cd95-fb6f-40d4-a93c-6b26dc3e0974",
          "Value": {
            "S": "Test_a658e60b-850c-4f28-9ae5-78e990a774be",
            "I": 536667375,
            "G": "4ad044a4-0f1f-442e-976c-b67b6f9d3ec8"
          }
        }
      ]
    },
    {
      "name": "urn:MyQueue",
      "values": [
        "8e84f808-7502-492b-a381-9a2892ffb8ae",
        "339f9a62-13f2-4bac-8058-18cce8d6f88d",
        "35c643c2-0c0f-43dc-9514-775eb4f486c4"
      ]
    }
  ]
}
```
