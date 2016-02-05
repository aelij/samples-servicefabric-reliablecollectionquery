using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace SampleStatefulService
{
    internal sealed class SampleStatefulService : StatefulService, IReliableServiceQuery
    {
        private readonly ReliableServiceQuery _reliableServiceQuery;

        public SampleStatefulService()
        {
            _reliableServiceQuery = new ReliableServiceQuery(() => StateManager);
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[] { new ServiceReplicaListener(p => new ServiceRemotingListener<SampleStatefulService>(p, this)) };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // put in some sample data

            var myStateDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, MyState>>("MyState");
            using (var tx = StateManager.CreateTransaction())
            {
                for (int i = 0; i < 100; i++)
                {
                    await myStateDictionary.AddAsync(tx, Guid.NewGuid(),
                        new MyState
                        {
                            G = ServiceInitializationParameters.PartitionId,
                            S = "Test_" + Guid.NewGuid(),
                            I = Environment.TickCount
                        });
                }

                await tx.CommitAsync();
            }

            var myQueue = await StateManager.GetOrAddAsync<IReliableQueue<Guid>>("MyQueue");
            using (var tx = StateManager.CreateTransaction())
            {
                for (int i = 0; i < 10; i++)
                {
                    await myQueue.EnqueueAsync(tx, Guid.NewGuid());
                }

                await tx.CommitAsync();
            }
        }

        public Task<IList<Uri>> GetCollections()
        {
            return _reliableServiceQuery.GetCollections();
        }

        public Task<IList<string>> GetCollectionData(Uri name)
        {
            return _reliableServiceQuery.GetCollectionData(name);
        }
    }

    [DataContract]
    public class MyState
    {
        [DataMember]
        public string S { get; set; }
        [DataMember]
        public int I { get; set; }
        [DataMember]
        public Guid G { get; set; }
    }
}
