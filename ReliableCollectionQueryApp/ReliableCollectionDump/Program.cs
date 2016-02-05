using System;
using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using System.Fabric.Query;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using Common;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Newtonsoft.Json;

namespace ReliableCollectionDump
{
    [SuppressMessage("ReSharper", "ConsiderUsingConfigureAwait")]
    class Program
    {
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        class Options
        {
            [Option("AppName")]
            public string ApplicationName { get; set; }

            [Option("ServiceNameFilter")]
            public string ServiceNameFilter { get; set; }
        }

        static void Main(string[] args)
        {
            var options = new Options();
            if (Parser.Default.ParseArgumentsStrict(args, options))
            {
                new Program(options).Run().GetAwaiter().GetResult();
            }
        }

        private readonly Options _options;

        private readonly FabricClient _fabricClient;

        private Program(Options options)
        {
            _options = options;
            _fabricClient = new FabricClient();
        }

        private void LogError(string message)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Log(message);
            Console.ForegroundColor = color;
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
        }

        private async Task Run()
        {
            var apps = await GetApps();

            foreach (var app in apps)
            {
                await DumpApp(app);
            }
        }

        private async Task<Uri[]> GetApps()
        {
            var apps = _options.ApplicationName != null
                ? new[] { new Uri(_options.ApplicationName) }
                : (await _fabricClient.QueryManager.GetApplicationListAsync()).Select(x => x.ApplicationName).ToArray();
            return apps;
        }

        private async Task DumpApp(Uri appName)
        {
            Log($"Dumping app {appName}");

            var services = await _fabricClient.QueryManager.GetServiceListAsync(appName);

            var serviceTasks = services
                .Where(service => service.ServiceKind == ServiceKind.Stateful &&
                    (_options.ServiceNameFilter == null ||
                     Regex.IsMatch(service.ServiceName.ToString(), _options.ServiceNameFilter)))
                .Select(service => DumpService(service.ServiceName)).ToArray();

            await Task.WhenAll(serviceTasks);
        }

        private async Task DumpService(Uri serviceName)
        {
            Log($"Dumping service {serviceName}");

            var partitions = await _fabricClient.QueryManager.GetPartitionListAsync(serviceName);

            if (partitions.Count == 0) return;

            try
            {
                foreach (var partition in partitions)
                {
                    IReliableServiceQuery proxy;
                    switch (partition.PartitionInformation.Kind)
                    {
                        case ServicePartitionKind.Singleton:
                            proxy = ServiceProxy.Create<IReliableServiceQuery>(serviceName);
                            break;
                        case ServicePartitionKind.Named:
                            proxy = ServiceProxy.Create<IReliableServiceQuery>(
                                ((NamedPartitionInformation)partition.PartitionInformation).Name, serviceName);
                            break;
                        case ServicePartitionKind.Int64Range:
                            proxy = ServiceProxy.Create<IReliableServiceQuery>(
                                ((Int64RangePartitionInformation)partition.PartitionInformation).LowKey, serviceName);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    await DumpPartition(serviceName, partition.PartitionInformation.Id, proxy);
                }
            }
            catch (Exception e)
            {
                if (e is AggregateException) e = ((AggregateException)e).InnerException;
                LogError($"Error dumping service {serviceName}: {e.Message}");
            }
        }

        private async Task DumpPartition(Uri serviceName, Guid partitionId, IReliableServiceQuery proxy)
        {
            Log($"Dumping partition {serviceName}/{partitionId}");

            var collections = await proxy.GetCollections();

            if (collections.Count == 0) return;

            using (var fileStream = new FileStream(GetFileName(serviceName, partitionId), FileMode.Create))
            using (var streamWriter = new StreamWriter(fileStream))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("serviceName");
                jsonWriter.WriteValue(serviceName.ToString());
                jsonWriter.WritePropertyName("partitionId");
                jsonWriter.WriteValue(partitionId.ToString());
                jsonWriter.WritePropertyName("collections");
                jsonWriter.WriteStartArray();

                foreach (var collection in collections)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("name");
                    jsonWriter.WriteValue(collection.ToString());
                    jsonWriter.WritePropertyName("values");

                    try
                    {
                        await DumpCollection(collection, proxy, jsonWriter);
                    }
                    catch (Exception e)
                    {
                        Log($"Error dumping collection on partition {serviceName}/{partitionId}: {e.Message}");
                    }

                    jsonWriter.WriteEndObject();
                }

                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
            }
        }

        private async Task DumpCollection(Uri collection, IReliableServiceQuery proxy, JsonTextWriter jsonWriter)
        {
            Log($"Dumping collection {collection}");

            var collectionData = await proxy.GetCollectionData(collection);

            jsonWriter.WriteStartArray();

            foreach (var item in collectionData)
            {
                using (var reader = new JsonTextReader(new StringReader(item)))
                {
                    jsonWriter.WriteToken(reader);
                }
            }

            jsonWriter.WriteEndArray();
        }

        private static string GetFileName(Uri serviceName, Guid partitionId)
        {
            var safeServiceName = serviceName.GetComponents(UriComponents.Host | UriComponents.Path, UriFormat.SafeUnescaped).Trim('/');
            foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
            {
                safeServiceName = safeServiceName.Replace(invalidFileNameChar, '_');
            }
            return $"{safeServiceName}_{partitionId}.json";
        }
    }
}
