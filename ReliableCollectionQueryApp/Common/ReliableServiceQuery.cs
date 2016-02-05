using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Newtonsoft.Json;

namespace Common
{
    public class ReliableServiceQuery : IReliableServiceQuery
    {
        private readonly Func<IReliableStateManager> _stateManagerFactory;

        public ReliableServiceQuery(Func<IReliableStateManager> stateManagerFactory)
        {
            _stateManagerFactory = stateManagerFactory;
        }

        public Task<IList<Uri>> GetCollections()
        {
            // reflection needed since ReliableStateManager.GetEnumerator() throws a NotImplementedException
            var stateManager = typeof(ReliableStateManager).GetProperty("Impl", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(_stateManagerFactory());
            var replicator = stateManager.GetType().GetProperty("Replicator", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(stateManager);
            var enumerable = (IEnumerable<object>)replicator.GetType().GetMethod("CreateEnumerable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(replicator, new object[] { false });
            var collections = enumerable.OfType<IReliableState>().Select(x => x.Name).ToArray();
            return Task.FromResult<IList<Uri>>(collections);
        }

        public async Task<IList<string>> GetCollectionData(Uri name)
        {
            var state = await _stateManagerFactory().TryGetAsync<IReliableState>(name).ConfigureAwait(false);
            var enumerable = state.Value as IEnumerable;
            if (enumerable == null)
            {
                throw new ArgumentException("Invalid name", nameof(name));
            }
            var collectionData = enumerable.Cast<object>().Select(JsonConvert.SerializeObject).ToArray();
            return collectionData;
        }
    }
}