//using CachingMVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TryMailAndSMSMVC.Services {
    public class CasheService {
        private IMemoryCache cache;
        public CasheService(IMemoryCache memoryCache) {
            cache = memoryCache;
        }

        public void SetObjInCache(object id, object value, double time) {
            cache.Set(id, value, new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(time)
            });
        }

        public string GetObjFromCache(object objId) {
            string outObj = null;
            if(!cache.TryGetValue(objId, out outObj)) {
                if(objId == null) {
                    return "not found this objId";
                }
            }
            return outObj;
        }
    }
}
