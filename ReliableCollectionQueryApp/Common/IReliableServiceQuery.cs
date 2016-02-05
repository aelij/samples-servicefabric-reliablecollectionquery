using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common
{
    public interface IReliableServiceQuery : IService
    {
        Task<IList<Uri>> GetCollections();

        Task<IList<string>> GetCollectionData(Uri name);
    }
}
